using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

internal sealed class KeywordOverlapReranker : IRagReranker
{
    public Task<RagRerankResult> RerankAsync(
        RagRerankRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queryTerms = Terms(request.Query);
        var candidates = request.Candidates.Select(candidate =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contentTerms = Terms(candidate.Content);
            var overlap = queryTerms.Count == 0 ? 0 : queryTerms.Count(contentTerms.Contains);
            var relevance = queryTerms.Count == 0 ? 0 : (double)overlap / queryTerms.Count;
            var answerability = overlap > 0 ? RagAnswerability.Answerable : RagAnswerability.NotAnswerable;
            return new RagRerankCandidateResult(
                candidate.DocumentId, candidate.ChunkId, relevance, answerability);
        }).ToArray();
        var aggregate = candidates.Any(candidate => candidate.Answerability == RagAnswerability.Answerable)
            ? RagAnswerability.Answerable
            : RagAnswerability.NotAnswerable;
        return Task.FromResult(new RagRerankResult(candidates, aggregate));
    }

    private static HashSet<string> Terms(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Trim('.', ',', ':', ';', '!', '?', '"', '\'', '(', ')').ToUpperInvariant())
            .Where(term => term.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
}
