using System.Text.Json;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Models.Reranking;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class AgentChatRagStreamEventMapperTests
{
    [Fact]
    // Ensures a RAG search started event is projected with its complete transport identity and request data.
    public void FromExecutionEvent_ShouldMapRagSearchStarted()
    {
        var executionEvent = AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
            "correlation-1", "agent-1", "conversation-1", "documents", "original", "rewritten", 20));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("rag_search_started", streamEvent.Type);
        Assert.Null(streamEvent.Content);
        Assert.Equal("agent-1", streamEvent.RagSearch!.AgentId);
        Assert.Equal("conversation-1", streamEvent.RagSearch.ConversationId);
        Assert.Equal("correlation-1", streamEvent.RagSearch.CorrelationId);
        Assert.Equal("documents", streamEvent.RagSearch.IndexName);
        Assert.Equal("original", streamEvent.RagSearch.OriginalQuery);
        Assert.Equal("rewritten", streamEvent.RagSearch.EffectiveQuery);
        Assert.Equal(20, streamEvent.RagSearch.RequestedCandidateCount);
        Assert.Null(streamEvent.ToolCallId);
    }

    [Fact]
    // Ensures a completed RAG search projects every count, score, selection, rejection, duration, and outcome field.
    public void FromExecutionEvent_ShouldMapRagSearchCompleted()
    {
        var completed = new RagSearchCompleted(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, 2, 1, 1,
            [new RagSearchSelectedResult("document-1", "chunk-1", 0.9, 0.95, "cosine-similarity", true,
                provenance: new RagRetrievalProvenance
                {
                    Mode = RagRetrievalMode.Hybrid,
                    SemanticRank = 1,
                    LexicalRank = 2,
                    ReciprocalRankFusionScore = 0.03,
                    FusedRank = 1,
                })],
            [new RagSearchRejectedResult("document-2", "chunk-2", 0.4, 0.3, RagResultRejectionReason.BelowMinimumRelevance)],
            5, TimeSpan.FromMilliseconds(125), 0.9, 0.95, null,
            retrievalMode: RagRetrievalMode.Hybrid,
            semanticCandidateCount: 5, lexicalCandidateCount: 4, fusedCandidateCount: 7);

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed));

        Assert.Equal("rag_search_completed", streamEvent.Type);
        var payload = streamEvent.RagSearch!;
        Assert.Equal(2, payload.ActualCandidateCount);
        Assert.Equal(1, payload.AcceptedCount);
        Assert.Equal(1, payload.RejectedCount);
        Assert.Equal(5, payload.MaximumAcceptedResultCount);
        Assert.Equal(0.9, payload.TopRawScore);
        Assert.Equal(0.95, payload.TopNormalizedRelevance);
        Assert.Equal(TimeSpan.FromMilliseconds(125), payload.Duration);
        Assert.Equal("document-1", Assert.Single(payload.SelectedResults!).DocumentId);
        var rejected = Assert.Single(payload.RejectedResults!);
        Assert.Equal("chunk-2", rejected.ChunkId);
        Assert.Equal(RagResultRejectionReason.BelowMinimumRelevance, rejected.Reason);
        Assert.Equal(0.4, rejected.RawScore);
        Assert.Equal(0.3, rejected.NormalizedRelevance);
        Assert.Null(payload.NoContextReason);
        Assert.Null(payload.FailureClassification);
        Assert.Equal(RagRetrievalMode.Hybrid, payload.RetrievalMode);
        Assert.Equal(5, payload.SemanticCandidateCount);
        Assert.Equal(4, payload.LexicalCandidateCount);
        Assert.Equal(7, payload.FusedCandidateCount);
        Assert.Equal(1, Assert.Single(payload.SelectedResults!).Provenance?.SemanticRank);
    }

    [Fact]
    // Ensures a failed RAG search remains distinct from terminal agent failure and carries only structured classification data.
    public void FromExecutionEvent_ShouldMapRagSearchFailed()
    {
        var failed = new RagSearchFailed(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, RetrievalErrorCode.VectorStoreQueryFailed, TimeSpan.FromSeconds(2));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(failed));

        Assert.Equal("rag_search_failed", streamEvent.Type);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, streamEvent.RagSearch!.FailureClassification);
        Assert.Equal(TimeSpan.FromSeconds(2), streamEvent.RagSearch.Duration);
        Assert.Null(streamEvent.RagSearch.NoContextReason);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    // Ensures RAG transport JSON uses stable discriminators and enum names without leaking content or non-finite scores.
    public void FromExecutionEvent_ShouldSerializeRagSearchWithoutSensitivePayload()
    {
        var completed = new RagSearchCompleted(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, 1, 0, 1, [],
            [new RagSearchRejectedResult("document-1", "chunk-1", double.NaN, double.NaN, RagResultRejectionReason.InvalidScore)],
            5, TimeSpan.FromMilliseconds(10), null, null, RagNoContextReason.CandidatesRejected);
        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed));

        var json = JsonSerializer.Serialize(streamEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"type\":\"rag_search_completed\"", json);
        Assert.Contains("\"reason\":\"InvalidScore\"", json);
        Assert.Contains("\"noContextReason\":\"CandidatesRejected\"", json);
        Assert.Contains("\"correlationId\":\"correlation-1\"", json);
        Assert.Contains("\"duration\":\"00:00:00.0100000\"", json);
        Assert.DoesNotContain("effectiveQuery", json);
        Assert.DoesNotContain("rawScore", json);
        Assert.DoesNotContain("normalizedRelevance", json);
        Assert.DoesNotContain("content preview", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Verifies lexical-only SSE omits semantic score meanings while retaining lexical and RRF provenance.
    public void FromExecutionEvent_ShouldPreserveLexicalOnlyScoreDistinction()
    {
        var completed = new RagSearchCompleted(
            "correlation", "agent", "conversation", "documents", "query", null,
            1, 1, 1, 0,
            [new RagSearchSelectedResult("document", "chunk", provenance: new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Hybrid,
                LexicalRank = 1,
                LexicalRawScore = 1.2,
                ReciprocalRankFusionScore = 1d / 61d,
                FusedRank = 1,
            })],
            [], 1, TimeSpan.Zero, null, null, null,
            retrievalMode: RagRetrievalMode.Hybrid,
            semanticCandidateCount: 0, lexicalCandidateCount: 1, fusedCandidateCount: 1);

        var payload = AgentChatStreamEventMapper
            .FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed)).RagSearch!;
        var selected = Assert.Single(payload.SelectedResults!);
        Assert.Null(selected.RawScore);
        Assert.Null(selected.NormalizedRelevance);
        Assert.Null(selected.Metric);
        Assert.Null(selected.HigherIsBetter);
        Assert.Equal(1.2, selected.Provenance?.LexicalRawScore);
        Assert.NotNull(selected.Provenance?.ReciprocalRankFusionScore);
    }

    [Fact]
    // Verifies unknown legacy counts are omitted from serialized SSE instead of appearing as factual zeros.
    public void FromExecutionEvent_ShouldOmitUnknownCandidateCounts()
    {
        var completed = new RagSearchCompleted(
            "correlation", "agent", "conversation", "documents", "query", null,
            1, 1, 1, 0,
            [new RagSearchSelectedResult("document", "chunk", 0.8, 0.9, "cosine-similarity", true)],
            [], 1, TimeSpan.Zero, 0.8, 0.9, null);

        var json = JsonSerializer.Serialize(
            AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("semanticCandidateCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("lexicalCandidateCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fusedCandidateCount", json, StringComparison.Ordinal);
    }

    [Fact]
    // Verifies successful reranking is serialized as the complete stable JSON contract consumed by Dashboard.
    public void FromExecutionEvent_ShouldSerializeSuccessfulRerankingMetadata()
    {
        using var json = SerializeCompleted(CreateCompleted(reranking: new RagRerankingMetadata(
            requested: true,
            ran: true,
            candidateCount: 2,
            duration: TimeSpan.FromMilliseconds(18),
            outcome: RagRerankingOutcome.Succeeded,
            failurePolicy: RagRerankerFailurePolicy.UseOriginalOrder,
            answerability: RagAnswerability.Answerable,
            candidates:
            [
                new RagRerankedCandidateMetadata("document-2", "chunk-2", 2, 1, 0.91, RagAnswerability.Answerable),
                new RagRerankedCandidateMetadata("document-1", "chunk-1", 1, 2, 0.72, RagAnswerability.Unknown),
            ])));

        var reranking = json.RootElement.GetProperty("ragSearch").GetProperty("reranking");
        Assert.True(reranking.GetProperty("requested").GetBoolean());
        Assert.True(reranking.GetProperty("ran").GetBoolean());
        Assert.Equal(2, reranking.GetProperty("candidateCount").GetInt32());
        Assert.Equal("00:00:00.0180000", reranking.GetProperty("duration").GetString());
        Assert.Equal("Succeeded", reranking.GetProperty("outcome").GetString());
        Assert.Equal("UseOriginalOrder", reranking.GetProperty("failurePolicy").GetString());
        Assert.Equal("Answerable", reranking.GetProperty("answerability").GetString());
        Assert.False(reranking.GetProperty("timedOut").GetBoolean());
        var firstCandidate = reranking.GetProperty("candidates")[0];
        Assert.Equal(2, firstCandidate.GetProperty("originalRank").GetInt32());
        Assert.Equal(1, firstCandidate.GetProperty("rerankRank").GetInt32());
        Assert.Equal(0.91, firstCandidate.GetProperty("rerankRelevance").GetDouble());
        Assert.Equal("Answerable", firstCandidate.GetProperty("answerability").GetString());
    }

    [Fact]
    // Verifies an unanswerable rerank serializes both the no-context outcome and its context exclusion reason by name.
    public void FromExecutionEvent_ShouldSerializeNotAnswerableNoContextOutcome()
    {
        var reranking = new RagRerankingMetadata(
            true, true, 1, TimeSpan.FromMilliseconds(4), RagRerankingOutcome.Succeeded,
            RagRerankerFailurePolicy.UseOriginalOrder, RagAnswerability.NotAnswerable,
            [new RagRerankedCandidateMetadata("document-1", "chunk-1", 1, 1, 0.2, RagAnswerability.NotAnswerable)]);
        using var json = SerializeCompleted(CreateCompleted(
            noContextReason: RagNoContextReason.NotAnswerable,
            contextExcludedResults:
            [
                new RagSearchContextExcludedResult(
                    "document-1", "chunk-1", RagContextSelectionExclusionReason.NotAnswerable, 40),
            ],
            reranking: reranking));

        var ragSearch = json.RootElement.GetProperty("ragSearch");
        Assert.Equal("NotAnswerable", ragSearch.GetProperty("noContextReason").GetString());
        Assert.Equal("NotAnswerable", ragSearch.GetProperty("contextExcludedResults")[0].GetProperty("reason").GetString());
        Assert.Equal("NotAnswerable", ragSearch.GetProperty("reranking").GetProperty("answerability").GetString());
    }

    [Fact]
    // Verifies a timed-out reranker serializes fallback status, timeout, and only its safe failure classification.
    public void FromExecutionEvent_ShouldSerializeTimeoutFallback()
    {
        using var json = SerializeCompleted(CreateCompleted(reranking: new RagRerankingMetadata(
            true, true, 1, TimeSpan.FromSeconds(5), RagRerankingOutcome.Fallback,
            RagRerankerFailurePolicy.UseOriginalOrder, RagAnswerability.Unknown,
            timedOut: true, failureCode: "Timeout")));

        var reranking = json.RootElement.GetProperty("ragSearch").GetProperty("reranking");
        Assert.Equal("Fallback", reranking.GetProperty("outcome").GetString());
        Assert.Equal("UseOriginalOrder", reranking.GetProperty("failurePolicy").GetString());
        Assert.True(reranking.GetProperty("timedOut").GetBoolean());
        Assert.Equal("Timeout", reranking.GetProperty("failureCode").GetString());
    }

    [Fact]
    // Verifies the fail policy and blocked reranking outcome remain explicit in serialized observability JSON.
    public void FromExecutionEvent_ShouldSerializeFailPolicy()
    {
        using var json = SerializeCompleted(CreateCompleted(reranking: new RagRerankingMetadata(
            true, true, 1, TimeSpan.FromMilliseconds(9), RagRerankingOutcome.Failed,
            RagRerankerFailurePolicy.Fail, RagAnswerability.Unknown,
            failureCode: "Unavailable")));

        var reranking = json.RootElement.GetProperty("ragSearch").GetProperty("reranking");
        Assert.Equal("Failed", reranking.GetProperty("outcome").GetString());
        Assert.Equal("Fail", reranking.GetProperty("failurePolicy").GetString());
        Assert.Equal("Unavailable", reranking.GetProperty("failureCode").GetString());
    }

    [Fact]
    // Verifies reranking observability JSON cannot expose provider diagnostics or source chunk content.
    public void FromExecutionEvent_ShouldNotSerializeRerankingProviderDetailsOrChunkContent()
    {
        var completed = CreateCompleted(reranking: new RagRerankingMetadata(
            true, true, 1, TimeSpan.FromMilliseconds(3), RagRerankingOutcome.Fallback,
            RagRerankerFailurePolicy.UseOriginalOrder, RagAnswerability.Unknown,
            [new RagRerankedCandidateMetadata("document-1", "chunk-1", 1, 1, 0.4, RagAnswerability.Unknown)],
            failureCode: "ProviderFailure"));

        var serialized = JsonSerializer.Serialize(
            AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var json = JsonDocument.Parse(serialized);
        var reranking = json.RootElement.GetProperty("ragSearch").GetProperty("reranking");
        var candidate = reranking.GetProperty("candidates")[0];

        Assert.Equal(
            ["answerability", "chunkId", "documentId", "originalRank", "rerankRank", "rerankRelevance"],
            candidate.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(
            ["answerability", "candidateCount", "candidates", "duration", "failureCode", "failurePolicy", "outcome", "ran", "requested", "timedOut"],
            reranking.EnumerateObject().Select(property => property.Name).Order());
        Assert.False(reranking.TryGetProperty("providerResponse", out _));
        Assert.False(reranking.TryGetProperty("exception", out _));
        Assert.False(reranking.TryGetProperty("stackTrace", out _));
        Assert.False(candidate.TryGetProperty("content", out _));
        Assert.False(candidate.TryGetProperty("metadata", out _));
        Assert.Equal("ProviderFailure", reranking.GetProperty("failureCode").GetString());
    }

    private static RagSearchCompleted CreateCompleted(
        RagNoContextReason? noContextReason = null,
        IReadOnlyList<RagSearchContextExcludedResult>? contextExcludedResults = null,
        RagRerankingMetadata? reranking = null) =>
        new(
            "correlation", "agent", "conversation", "documents", "question", null,
            2, 1, 1, 0,
            noContextReason is null ? [new RagSearchSelectedResult("document-1", "chunk-1")] : [],
            [], 2, TimeSpan.FromMilliseconds(10), null, null, noContextReason,
            contextExcludedResults: contextExcludedResults,
            reranking: reranking);

    private static JsonDocument SerializeCompleted(RagSearchCompleted completed)
    {
        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed));
        return JsonDocument.Parse(JsonSerializer.Serialize(streamEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
