using System.Diagnostics;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Reranking;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

/// <summary>Executes and validates the optional second-stage reranking pipeline.</summary>
internal static class RagRerankingProcessor
{
    /// <summary>Reranks accepted retrieval results according to the configured failure policy.</summary>
    /// <param name="query">The original retrieval query.</param>
    /// <param name="acceptedResults">The candidates accepted by the retrieval stage.</param>
    /// <param name="options">The configured reranking behavior.</param>
    /// <param name="reranker">The optional reranker implementation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The ordered results and safe reranking metadata.</returns>
    public static async Task<RagRerankingExecution> ExecuteAsync(
        string query,
        IReadOnlyList<RagSearchResult> acceptedResults,
        RagRerankingOptions options,
        IRagReranker? reranker,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return new(acceptedResults, new RagRerankingMetadata(false, false, 0, TimeSpan.Zero,
                RagRerankingOutcome.Disabled, options.FailurePolicy, RagAnswerability.Unknown), false);

        var bounded = acceptedResults.Take(options.MaximumCandidates).ToArray();
        if (bounded.Length == 0)
            return new(acceptedResults, new RagRerankingMetadata(true, false, 0, TimeSpan.Zero,
                RagRerankingOutcome.Succeeded, options.FailurePolicy, RagAnswerability.Unknown), false);

        if (reranker is null)
            return Failure(acceptedResults, options, false, TimeSpan.Zero, "RerankerUnavailable", timedOut: false);

        var request = new RagRerankRequest(query, bounded.Select((result, index) =>
            new RagRerankCandidate(result.Chunk.DocumentId, result.Chunk.Id, result.Chunk.Content, index + 1)).ToArray());
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);
            var response = await reranker.RerankAsync(request, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            var validated = Validate(response, bounded);
            var orderedHead = validated
                .OrderByDescending(item => item.Result.Relevance)
                .ThenBy(item => item.OriginalRank)
                .ThenBy(item => item.SearchResult.Chunk.DocumentId, StringComparer.Ordinal)
                .ThenBy(item => item.SearchResult.Chunk.Id, StringComparer.Ordinal)
                .ToArray();
            var candidateMetadata = orderedHead.Select((item, index) => new RagRerankedCandidateMetadata(
                item.SearchResult.Chunk.DocumentId, item.SearchResult.Chunk.Id, item.OriginalRank, index + 1,
                item.Result.Relevance, item.Result.Answerability)).ToArray();
            var ordered = orderedHead.Select(item => item.SearchResult)
                .Concat(acceptedResults.Skip(bounded.Length)).ToArray();
            return new(ordered, new RagRerankingMetadata(true, true, bounded.Length, stopwatch.Elapsed,
                RagRerankingOutcome.Succeeded, options.FailurePolicy, response.Answerability, candidateMetadata), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(acceptedResults, options, true, stopwatch.Elapsed, "RerankerTimeout", timedOut: true);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            return Failure(acceptedResults, options, true, stopwatch.Elapsed, "RerankerFailed", timedOut: false);
        }
    }

    private static RagRerankingExecution Failure(
        IReadOnlyList<RagSearchResult> original,
        RagRerankingOptions options,
        bool ran,
        TimeSpan duration,
        string failureCode,
        bool timedOut)
    {
        var blocks = options.FailurePolicy == RagRerankerFailurePolicy.Fail;
        return new(original, new RagRerankingMetadata(true, ran, Math.Min(original.Count, options.MaximumCandidates),
            duration, blocks ? RagRerankingOutcome.Failed : RagRerankingOutcome.Fallback,
            options.FailurePolicy, RagAnswerability.Unknown, timedOut: timedOut, failureCode: failureCode), blocks);
    }

    private static IReadOnlyList<ValidatedCandidate> Validate(
        RagRerankResult response,
        IReadOnlyList<RagSearchResult> requested)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!Enum.IsDefined(response.Answerability)) throw new InvalidOperationException("Invalid aggregate answerability.");
        if (response.Candidates is null || response.Candidates.Count != requested.Count)
            throw new InvalidOperationException("Reranker output must contain every requested candidate exactly once.");

        var requestedByIdentity = requested
            .Select((result, index) => new
            {
                Key = Identity(result.Chunk.DocumentId, result.Chunk.Id),
                Result = result,
                Rank = index + 1,
            })
            .ToDictionary(item => item.Key, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<ValidatedCandidate>(requested.Count);
        foreach (var item in response.Candidates)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.DocumentId) || string.IsNullOrWhiteSpace(item.ChunkId))
                throw new InvalidOperationException("Reranker output contains a missing candidate identity.");
            if (!double.IsFinite(item.Relevance) || item.Relevance is < 0 or > 1)
                throw new InvalidOperationException("Rerank relevance must be finite and between zero and one.");
            if (!Enum.IsDefined(item.Answerability))
                throw new InvalidOperationException("Invalid candidate answerability.");
            var key = Identity(item.DocumentId, item.ChunkId);
            if (!seen.Add(key)) throw new InvalidOperationException("Reranker output contains a duplicate candidate.");
            if (!requestedByIdentity.TryGetValue(key, out var requestedItem))
                throw new InvalidOperationException("Reranker output contains an unknown candidate.");
            validated.Add(new(requestedItem.Result, requestedItem.Rank, item));
        }
        return validated;
    }

    private static string Identity(string documentId, string chunkId) => $"{documentId.Length}:{documentId}{chunkId}";

    private sealed record ValidatedCandidate(
        RagSearchResult SearchResult,
        int OriginalRank,
        RagRerankCandidateResult Result);
}

/// <summary>Captures the result of a reranking stage before agent execution continues.</summary>
/// <param name="OrderedResults">The results ordered for subsequent processing.</param>
/// <param name="Metadata">The safe observable reranking metadata.</param>
/// <param name="BlocksExecution">Indicates whether the configured failure policy blocks execution.</param>
internal sealed record RagRerankingExecution(
    IReadOnlyList<RagSearchResult> OrderedResults,
    RagRerankingMetadata Metadata,
    bool BlocksExecution);
