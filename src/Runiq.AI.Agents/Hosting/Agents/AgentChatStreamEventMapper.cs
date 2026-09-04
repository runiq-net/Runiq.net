using Runiq.AI.Agents;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Agent execution olaylarini Dashboard'un bekledigi stream DTO formatina cevirir.
/// </summary>
internal static class AgentChatStreamEventMapper
{
    public static AgentChatStreamEvent FromExecutionEvent(AgentExecutionEvent executionEvent)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);

        return executionEvent.Kind switch
        {
            AgentExecutionEventKind.RagSearch => FromRagSearchEvent(executionEvent.RagSearch!),

            AgentExecutionEventKind.AssistantDelta => new AgentChatStreamEvent(
                Type: "assistant_delta",
                Content: executionEvent.Content),

            AgentExecutionEventKind.ToolCallStarted => new AgentChatStreamEvent(
                Type: "tool_call_started",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ArgumentsJson: executionEvent.ArgumentsJson),

            AgentExecutionEventKind.ToolCallCompleted => new AgentChatStreamEvent(
                Type: "tool_call_completed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                OutputJson: executionEvent.OutputJson),

            AgentExecutionEventKind.ToolCallFailed => new AgentChatStreamEvent(
                Type: "tool_call_failed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            AgentExecutionEventKind.Completed => new AgentChatStreamEvent(
                Type: "completed",
                Content: null)
            {
                Rag = executionEvent.Rag,
                Citations = executionEvent.Citations.Count == 0 ? null : executionEvent.Citations,
            },

            AgentExecutionEventKind.Failed => new AgentChatStreamEvent(
                Type: "failed",
                Content: executionEvent.Content,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage)
            {
                Rag = executionEvent.Rag,
            },

            _ => throw new ArgumentOutOfRangeException(
                nameof(executionEvent), executionEvent.Kind, "The execution event kind is not supported.")
        };
    }

    private static AgentChatStreamEvent FromRagSearchEvent(RagSearchEvent ragSearch) => ragSearch switch
    {
        RagSearchStarted started => CreateRagSearchEvent("rag_search_started", started) with
        {
            RagSearch = CreateStartedPayload(started),
        },
        RagSearchCompleted completed => CreateRagSearchEvent("rag_search_completed", completed) with
        {
            RagSearch = CreateCompletedPayload(completed),
        },
        RagSearchBlocked blocked => CreateRagSearchEvent("rag_search_blocked", blocked) with
        {
            RagSearch = CreateBlockedPayload(blocked),
        },
        RagSearchFailed failed => CreateRagSearchEvent("rag_search_failed", failed) with
        {
            RagSearch = CreateFailedPayload(failed),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(ragSearch), ragSearch.GetType(), "The RAG search event type is not supported."),
    };

    private static AgentChatStreamEvent CreateRagSearchEvent(string type, RagSearchEvent ragSearch) =>
        new(type, Content: null)
        {
            RagSearch = CreateCommonPayload(ragSearch),
        };

    private static AgentChatRagSearchEvent CreateCommonPayload(RagSearchEvent ragSearch) =>
        new(
            ragSearch.AgentId,
            ragSearch.ConversationId,
            ragSearch.CorrelationId,
            ragSearch.IndexName,
            ragSearch.OriginalQuery,
            ragSearch.EffectiveQuery,
            ragSearch.RequestedCandidateCount);

    private static AgentChatRagSearchEvent CreateStartedPayload(RagSearchStarted started) =>
        new(started.AgentId, started.ConversationId, started.CorrelationId, started.IndexName,
            started.OriginalQuery, started.EffectiveQuery, started.RequestedCandidateCount)
        {
            RetrievalMode = started.RetrievalMode,
        };

    private static AgentChatRagSearchEvent CreateCompletedPayload(RagSearchCompleted completed) =>
        new(
            completed.AgentId,
            completed.ConversationId,
            completed.CorrelationId,
            completed.IndexName,
            completed.OriginalQuery,
            completed.EffectiveQuery,
            completed.RequestedCandidateCount)
        {
            RetrievalMode = completed.RetrievalMode,
            SemanticCandidateCount = completed.SemanticCandidateCount,
            LexicalCandidateCount = completed.LexicalCandidateCount,
            FusedCandidateCount = completed.FusedCandidateCount,
            ActualCandidateCount = completed.ActualCandidateCount,
            AcceptedCount = completed.AcceptedCount,
            RejectedCount = completed.RejectedCount,
            MaximumAcceptedResultCount = completed.MaximumAcceptedResultCount,
            TopRawScore = completed.TopRawScore,
            TopNormalizedRelevance = completed.TopNormalizedRelevance,
            Duration = completed.Duration,
            SelectedResults = completed.SelectedResults.Select((result, index) => new AgentChatRagSelectedResult(
                    result.DocumentId, result.ChunkId, index,
                    result.RawScore is double rawScore && double.IsFinite(rawScore) ? rawScore : null,
                    IsNormalizedRelevance(result.NormalizedRelevance) ? result.NormalizedRelevance : null,
                    string.IsNullOrWhiteSpace(result.Metric) ? null : result.Metric,
                    string.IsNullOrWhiteSpace(result.Metric) ? null : result.HigherIsBetter,
                    result.ContentPreview, result.PreviewTruncated, result.Metadata.Count == 0 ? null : result.Metadata,
                    result.Provenance))
                .ToArray(),
            RejectedResults = completed.RejectedResults
                .Select(result => new AgentChatRagRejectedResult(
                    result.DocumentId,
                    result.ChunkId,
                    result.RawScore is double rawScore && double.IsFinite(rawScore) ? rawScore : null,
                    IsNormalizedRelevance(result.NormalizedRelevance) ? result.NormalizedRelevance : null,
                    result.Reason, result.ContentPreview, result.PreviewTruncated,
                    result.Metadata.Count == 0 ? null : result.Metadata, result.Provenance))
                .ToArray(),
            ContextExcludedResults = completed.ContextExcludedResults.Count == 0 ? null : completed.ContextExcludedResults,
            ContextBudget = completed.ContextBudget,
            Reranking = completed.Reranking,
            NoContextReason = completed.NoContextReason,
            IndexReadiness = completed.IndexReadiness,
            SafeFailureSummary = completed.SafeFailureSummary,
        };

    private static AgentChatRagSearchEvent CreateFailedPayload(RagSearchFailed failed) =>
        new(
            failed.AgentId,
            failed.ConversationId,
            failed.CorrelationId,
            failed.IndexName,
            failed.OriginalQuery,
            failed.EffectiveQuery,
            failed.RequestedCandidateCount)
        {
            Duration = failed.Duration,
            FailureClassification = failed.FailureClassification,
        };

    private static AgentChatRagSearchEvent CreateBlockedPayload(RagSearchBlocked blocked) =>
        new(blocked.AgentId, blocked.ConversationId, blocked.CorrelationId, blocked.IndexName,
            blocked.OriginalQuery, blocked.EffectiveQuery, blocked.RequestedCandidateCount)
        {
            Readiness = blocked.Readiness,
            BlockingReason = blocked.BlockingReason,
            SuggestedAction = blocked.SuggestedAction,
            LastUpdatedAt = blocked.LastUpdatedAt,
            ActiveOperationState = blocked.ActiveOperationState,
            ActiveOperationReason = blocked.ActiveOperationReason,
            Progress = blocked.Progress,
            SafeFailureSummary = blocked.SafeFailureSummary,
        };

    private static bool IsNormalizedRelevance(double? relevance) =>
        relevance is double value && double.IsFinite(value) && value is >= 0 and <= 1;
}
