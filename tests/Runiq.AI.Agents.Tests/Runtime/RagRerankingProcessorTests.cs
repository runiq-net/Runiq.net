using System.Diagnostics;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Reranking;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Runtime;

public sealed class RagRerankingProcessorTests
{
    // Proves disabled reranking preserves accepted order and performs no provider call.
    [Fact]
    public async Task ExecuteAsync_Disabled_PreservesOrderWithoutInvocation()
    {
        var reranker = new RecordingReranker(CreateResponse());
        var results = CreateResults();

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, new RagRerankingOptions(), reranker, CancellationToken.None);

        Assert.Equal(results, execution.OrderedResults);
        Assert.Equal(0, reranker.CallCount);
        Assert.Equal(RagRerankingOutcome.Disabled, execution.Metadata.Outcome);
    }

    // Proves enabled reranking uses a bounded set once and retains retrieval scores and provenance.
    [Fact]
    public async Task ExecuteAsync_Enabled_ReranksBoundedCandidatesAndPreservesRetrievalData()
    {
        var results = CreateResults();
        var reranker = new RecordingReranker(new RagRerankResult(
            [
                Candidate(results[0], 0.2),
                Candidate(results[1], 0.9),
            ],
            RagAnswerability.Answerable));
        var options = new RagRerankingOptions { Enabled = true, MaximumCandidates = 2 };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, options, reranker, CancellationToken.None);

        Assert.Equal(["b", "a", "c"], execution.OrderedResults.Select(item => item.Chunk.Id));
        Assert.Equal(1, reranker.CallCount);
        Assert.Equal(2, reranker.Request!.Candidates.Count);
        Assert.Equal(["a", "b"], reranker.Request.Candidates.Select(item => item.ChunkId));
        Assert.Equal("c", execution.OrderedResults[2].Chunk.Id);
        Assert.Equal(0.8, execution.OrderedResults[1].RawScore);
        Assert.Equal(2, execution.Metadata.Candidates[0].OriginalRank);
        Assert.Equal(1, execution.Metadata.Candidates[0].RerankRank);
    }

    // Proves equal rerank scores use original accepted rank as a stable deterministic tie-break.
    [Fact]
    public async Task ExecuteAsync_EqualScores_PreservesOriginalRank()
    {
        var results = CreateResults();
        var reranker = new RecordingReranker(new RagRerankResult(
            [Candidate(results[1], 0.5), Candidate(results[0], 0.5), Candidate(results[2], 0.5)],
            RagAnswerability.Answerable));
        var options = new RagRerankingOptions { Enabled = true };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, options, reranker, CancellationToken.None);

        Assert.Equal(["a", "b", "c"], execution.OrderedResults.Select(item => item.Chunk.Id));
    }

    // Proves invalid all-or-nothing output uses the exact original order and exposes fallback.
    [Fact]
    public async Task ExecuteAsync_DuplicateOutput_UsesObservableOriginalOrderFallback()
    {
        var results = CreateResults();
        var reranker = new RecordingReranker(new RagRerankResult(
            [Candidate(results[0], 0.9), Candidate(results[0], 0.8), Candidate(results[2], 0.7)],
            RagAnswerability.Answerable));
        var options = new RagRerankingOptions { Enabled = true };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, options, reranker, CancellationToken.None);

        Assert.Equal(results, execution.OrderedResults);
        Assert.Equal(RagRerankingOutcome.Fallback, execution.Metadata.Outcome);
        Assert.Equal("RerankerFailed", execution.Metadata.FailureCode);
        Assert.False(execution.BlocksExecution);
    }

    // Proves the fail policy blocks execution for invalid output instead of silently falling back.
    [Fact]
    public async Task ExecuteAsync_InvalidScoreWithFailPolicy_BlocksExecution()
    {
        var results = CreateResults();
        var reranker = new RecordingReranker(new RagRerankResult(
            [Candidate(results[0], double.NaN), Candidate(results[1], 0.8), Candidate(results[2], 0.7)],
            RagAnswerability.Answerable));
        var options = new RagRerankingOptions
        {
            Enabled = true,
            FailurePolicy = RagRerankerFailurePolicy.Fail,
        };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, options, reranker, CancellationToken.None);

        Assert.True(execution.BlocksExecution);
        Assert.Equal(RagRerankingOutcome.Failed, execution.Metadata.Outcome);
    }

    // Proves reranker timeout is distinct from caller cancellation and follows the fallback policy.
    [Fact]
    public async Task ExecuteAsync_Timeout_UsesConfiguredFallback()
    {
        var options = new RagRerankingOptions
        {
            Enabled = true,
            Timeout = TimeSpan.FromMilliseconds(10),
        };

        var reranker = new CancellingReranker();
        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", CreateResults(), options, reranker, CancellationToken.None);

        Assert.True(execution.Metadata.TimedOut);
        Assert.Equal("RerankerTimeout", execution.Metadata.FailureCode);
        Assert.Equal(RagRerankingOutcome.Fallback, execution.Metadata.Outcome);
        Assert.True(reranker.ProviderCancellationObserved);
    }

    // Proves caller cancellation propagates unchanged and is not converted into reranker timeout.
    [Fact]
    public async Task ExecuteAsync_CallerCancellation_Propagates()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var options = new RagRerankingOptions { Enabled = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RagRerankingProcessor.ExecuteAsync(
                "query", CreateResults(), options, new CancellingReranker(), source.Token));
    }

    // Proves an enabled reranker treats an empty accepted set as a successful no-op without provider invocation.
    [Fact]
    public async Task ExecuteAsync_EmptyAcceptedResults_CompletesWithoutInvocation()
    {
        var reranker = new RecordingReranker(CreateResponse());
        var options = new RagRerankingOptions { Enabled = true };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", [], options, reranker, CancellationToken.None);

        Assert.Empty(execution.OrderedResults);
        Assert.Equal(0, reranker.CallCount);
        Assert.False(execution.BlocksExecution);
        Assert.Equal(RagRerankingOutcome.Succeeded, execution.Metadata.Outcome);
        Assert.Equal(RagAnswerability.Unknown, execution.Metadata.Answerability);
        Assert.False(execution.Metadata.Ran);
        Assert.Equal(0, execution.Metadata.CandidateCount);
    }

    // Proves a missing reranker registration follows each configured failure policy deterministically.
    [Theory]
    [InlineData(RagRerankerFailurePolicy.UseOriginalOrder, RagRerankingOutcome.Fallback, false)]
    [InlineData(RagRerankerFailurePolicy.Fail, RagRerankingOutcome.Failed, true)]
    public async Task ExecuteAsync_MissingReranker_UsesConfiguredFailurePolicy(
        RagRerankerFailurePolicy failurePolicy,
        RagRerankingOutcome expectedOutcome,
        bool expectedToBlock)
    {
        var results = CreateResults();
        var options = new RagRerankingOptions { Enabled = true, FailurePolicy = failurePolicy };

        var execution = await RagRerankingProcessor.ExecuteAsync(
            "query", results, options, null, CancellationToken.None);

        Assert.Equal(results, execution.OrderedResults);
        Assert.Equal(expectedOutcome, execution.Metadata.Outcome);
        Assert.Equal("RerankerUnavailable", execution.Metadata.FailureCode);
        Assert.Equal(expectedToBlock, execution.BlocksExecution);
        Assert.False(execution.Metadata.Ran);
    }

    private static IReadOnlyList<RagSearchResult> CreateResults() =>
    [
        Create("a", "doc-a", 0.8),
        Create("b", "doc-b", 0.7),
        Create("c", "doc-c", 0.6),
    ];

    private static RagSearchResult Create(string id, string documentId, double score) => new()
    {
        Chunk = new RagChunk { Id = id, DocumentId = documentId, Content = $"content-{id}" },
        RawScore = score,
        Relevance = score,
        Metric = "cosine_similarity",
        HigherIsBetter = true,
    };

    private static RagRerankCandidateResult Candidate(RagSearchResult result, double score) =>
        new(result.Chunk.DocumentId, result.Chunk.Id, score, RagAnswerability.Answerable);

    private static RagRerankResult CreateResponse() =>
        new([], RagAnswerability.Unknown);

    private sealed class RecordingReranker(RagRerankResult result) : IRagReranker
    {
        public int CallCount { get; private set; }
        public RagRerankRequest? Request { get; private set; }

        public Task<RagRerankResult> RerankAsync(
            RagRerankRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class CancellingReranker : IRagReranker
    {
        public bool ProviderCancellationObserved { get; private set; }

        public async Task<RagRerankResult> RerankAsync(
            RagRerankRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new UnreachableException();
            }
            finally
            {
                ProviderCancellationObserved = cancellationToken.IsCancellationRequested;
            }
        }
    }
}
