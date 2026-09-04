using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Agents;

/// <summary>Provides the common identity and request data for a RAG search lifecycle payload.</summary>
public abstract class RagSearchEvent
{
    /// <summary>Initializes the common data for a RAG search lifecycle payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    protected RagSearchEvent(string correlationId, string agentId, string conversationId, string indexName,
        string? originalQuery, string? effectiveQuery, int requestedCandidateCount)
    {
        CorrelationId = RequireValue(correlationId, nameof(correlationId));
        AgentId = RequireValue(agentId, nameof(agentId));
        ConversationId = RequireValue(conversationId, nameof(conversationId));
        IndexName = RequireValue(indexName, nameof(indexName));
        OriginalQuery = string.IsNullOrWhiteSpace(originalQuery) ? null : originalQuery;
        EffectiveQuery = string.IsNullOrWhiteSpace(effectiveQuery) || string.Equals(OriginalQuery, effectiveQuery, StringComparison.Ordinal)
            ? null
            : effectiveQuery;
        RequestedCandidateCount = RequirePositive(requestedCandidateCount, nameof(requestedCandidateCount));
    }

    /// <summary>Gets the identifier shared by payloads from the same retrieval.</summary>
    public string CorrelationId { get; }
    /// <summary>Gets the identifier of the agent that initiated the retrieval.</summary>
    public string AgentId { get; }
    /// <summary>Gets the identifier of the conversation that contains the retrieval.</summary>
    public string ConversationId { get; }
    /// <summary>Gets the effective index queried by the runtime.</summary>
    public string IndexName { get; }
    /// <summary>Gets the original query received by the runtime.</summary>
    public string? OriginalQuery { get; }
    /// <summary>Gets the effective query when it differs from <see cref="OriginalQuery"/>; otherwise, gets <see langword="null"/>.</summary>
    public string? EffectiveQuery { get; }
    /// <summary>Gets the maximum number of candidates requested from retrieval.</summary>
    public int RequestedCandidateCount { get; }

    private static string RequireValue(string value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName)
        : value;
    internal static int RequireNonNegative(int value, string parameterName) => value < 0
        ? throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.") : value;
    internal static int RequirePositive(int value, string parameterName) => value <= 0
        ? throw new ArgumentOutOfRangeException(parameterName, value, "The value must be greater than zero.") : value;
}

/// <summary>Indicates that a RAG retrieval operation has started.</summary>
public sealed class RagSearchStarted : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval started payload.</summary>
    /// <inheritdoc cref="RagSearchEvent(string, string, string, string, string, string?, int)"/>
    public RagSearchStarted(string correlationId, string agentId, string conversationId, string indexName,
        string? originalQuery, string? effectiveQuery, int requestedCandidateCount,
        RagRetrievalMode retrievalMode = RagRetrievalMode.Semantic)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        if (!Enum.IsDefined(retrievalMode)) throw new ArgumentOutOfRangeException(nameof(retrievalMode));
        RetrievalMode = retrievalMode;
    }

    /// <summary>Gets the effective retrieval mode.</summary>
    public RagRetrievalMode RetrievalMode { get; }
}

/// <summary>Identifies the safe action suggested when RAG retrieval cannot start.</summary>
public enum RagReadinessSuggestedAction
{
    /// <summary>Start the first ingestion operation.</summary>
    StartIngestion,
    /// <summary>Wait for the active ingestion operation.</summary>
    WaitForIngestion,
    /// <summary>Retry ingestion after a failed operation.</summary>
    RetryIngestion,
    /// <summary>Check the effective index configuration.</summary>
    CheckConfiguration
}

/// <summary>Provides a safe count-only ingestion progress snapshot for Agent Chat readiness.</summary>
public sealed record RagReadinessProgress
{
    /// <summary>Gets the number of discovered documents.</summary>
    public int DiscoveredDocuments { get; }
    /// <summary>Gets the number of processed documents.</summary>
    public int ProcessedDocuments { get; }
    /// <summary>Gets the number of failed documents.</summary>
    public int FailedDocuments { get; }

    /// <summary>Initializes a validated count-only progress snapshot.</summary>
    /// <param name="discoveredDocuments">The discovered document count.</param>
    /// <param name="processedDocuments">The processed document count.</param>
    /// <param name="failedDocuments">The failed document count.</param>
    public RagReadinessProgress(int discoveredDocuments, int processedDocuments, int failedDocuments)
    {
        if (discoveredDocuments < 0) throw new ArgumentOutOfRangeException(nameof(discoveredDocuments));
        if (processedDocuments < 0) throw new ArgumentOutOfRangeException(nameof(processedDocuments));
        if (failedDocuments < 0) throw new ArgumentOutOfRangeException(nameof(failedDocuments));
        if (processedDocuments > discoveredDocuments) throw new ArgumentException("Processed documents cannot exceed discovered documents.", nameof(processedDocuments));
        if (failedDocuments > processedDocuments) throw new ArgumentException("Failed documents cannot exceed processed documents.", nameof(failedDocuments));
        DiscoveredDocuments = discoveredDocuments;
        ProcessedDocuments = processedDocuments;
        FailedDocuments = failedDocuments;
    }
}

/// <summary>Indicates that retrieval was blocked by the effective index readiness state.</summary>
public sealed class RagSearchBlocked : RagSearchEvent
{
    /// <summary>Initializes a structured readiness-blocked lifecycle payload.</summary>
    /// <param name="correlationId">The identifier shared by the retrieval lifecycle.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="indexName">The effective index name.</param>
    /// <param name="originalQuery">The safely projected original query.</param>
    /// <param name="effectiveQuery">The safely projected effective query.</param>
    /// <param name="requestedCandidateCount">The requested candidate count.</param>
    /// <param name="readiness">The index readiness, or null when the index is not registered.</param>
    /// <param name="blockingReason">The provider-independent blocking reason.</param>
    /// <param name="suggestedAction">The suggested developer action.</param>
    /// <param name="lastUpdatedAt">The last runtime status update, when available.</param>
    /// <param name="activeOperationState">The active ingestion state, when available.</param>
    /// <param name="activeOperationReason">The active ingestion reason, when available.</param>
    /// <param name="progress">The active ingestion progress, when available.</param>
    /// <param name="safeFailureSummary">A bounded safe failure summary, when available.</param>
    public RagSearchBlocked(string correlationId, string agentId, string conversationId, string indexName,
        string? originalQuery, string? effectiveQuery, int requestedCandidateCount, RagIndexReadiness? readiness,
        string blockingReason, RagReadinessSuggestedAction suggestedAction, DateTimeOffset? lastUpdatedAt = null,
        RagIngestionOperationState? activeOperationState = null, RagIngestionOperationReason? activeOperationReason = null,
        RagReadinessProgress? progress = null, string? safeFailureSummary = null)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        if (readiness is not null && (!Enum.IsDefined(readiness.Value) || readiness is RagIndexReadiness.Ready or RagIndexReadiness.Degraded)) throw new ArgumentOutOfRangeException(nameof(readiness));
        if (!Enum.IsDefined(suggestedAction)) throw new ArgumentOutOfRangeException(nameof(suggestedAction));
        var expectedAction = readiness switch
        {
            null => RagReadinessSuggestedAction.CheckConfiguration,
            RagIndexReadiness.NotInitialized => RagReadinessSuggestedAction.StartIngestion,
            RagIndexReadiness.Initializing => RagReadinessSuggestedAction.WaitForIngestion,
            RagIndexReadiness.Failed => RagReadinessSuggestedAction.RetryIngestion,
            _ => throw new ArgumentOutOfRangeException(nameof(readiness))
        };
        if (suggestedAction != expectedAction) throw new ArgumentException("The suggested action does not match the readiness state.", nameof(suggestedAction));
        if (readiness != RagIndexReadiness.Initializing && (activeOperationState is not null || activeOperationReason is not null || progress is not null))
            throw new ArgumentException("Active operation data is valid only while the index is initializing.", nameof(activeOperationState));
        if (readiness != RagIndexReadiness.Failed && !string.IsNullOrWhiteSpace(safeFailureSummary))
            throw new ArgumentException("A safe failure summary is valid only for failed readiness.", nameof(safeFailureSummary));
        Readiness = readiness;
        BlockingReason = string.IsNullOrWhiteSpace(blockingReason) ? throw new ArgumentException("A blocking reason is required.", nameof(blockingReason)) : blockingReason;
        SuggestedAction = suggestedAction;
        LastUpdatedAt = lastUpdatedAt;
        ActiveOperationState = activeOperationState;
        ActiveOperationReason = activeOperationReason;
        Progress = progress;
        SafeFailureSummary = ValidateSafeFailureSummary(safeFailureSummary);
    }

    /// <summary>Gets the readiness, or null when the index is not registered.</summary>
    public RagIndexReadiness? Readiness { get; }
    /// <summary>Gets the provider-independent blocking reason.</summary>
    public string BlockingReason { get; }
    /// <summary>Gets the suggested developer action.</summary>
    public RagReadinessSuggestedAction SuggestedAction { get; }
    /// <summary>Gets the last runtime status update, when available.</summary>
    public DateTimeOffset? LastUpdatedAt { get; }
    /// <summary>Gets the active ingestion state, when available.</summary>
    public RagIngestionOperationState? ActiveOperationState { get; }
    /// <summary>Gets the active ingestion reason, when available.</summary>
    public RagIngestionOperationReason? ActiveOperationReason { get; }
    /// <summary>Gets safe active ingestion progress, when available.</summary>
    public RagReadinessProgress? Progress { get; }
    /// <summary>Gets the bounded safe failure summary, when available.</summary>
    public string? SafeFailureSummary { get; }

    private static string? ValidateSafeFailureSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 256 ? normalized : normalized[..256];
    }
}

/// <summary>Indicates that a RAG retrieval operation completed successfully, with or without accepted context.</summary>
public sealed class RagSearchCompleted : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval completed payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    /// <param name="actualCandidateCount">The number of candidates returned by retrieval.</param>
    /// <param name="acceptedCount">The number of candidates accepted as context.</param>
    /// <param name="rejectedCount">The number of candidates rejected by runtime policy.</param>
    /// <param name="selectedResults">The document and chunk pairs selected as context.</param>
    /// <param name="rejectedResults">The structured candidates rejected by runtime policy.</param>
    /// <param name="maximumAcceptedResultCount">The configured maximum number of accepted results.</param>
    /// <param name="duration">The elapsed retrieval duration.</param>
    /// <param name="topRawScore">The finite raw score of the highest-ranked valid candidate, when available.</param>
    /// <param name="topNormalizedRelevance">The normalized relevance of the highest-ranked candidate, when reliably available.</param>
    /// <param name="noContextReason">The verifiable reason no context was accepted, or null when context was accepted.</param>
    /// <param name="indexReadiness">Degraded readiness when retrieval continues on a usable previous state.</param>
    /// <param name="safeFailureSummary">A bounded safe ingestion failure summary for degraded readiness.</param>
    /// <param name="retrievalMode">The effective retrieval mode.</param>
    /// <param name="semanticCandidateCount">The authoritative semantic source count; zero is known and null means unavailable metadata.</param>
    /// <param name="lexicalCandidateCount">The authoritative lexical source count; zero is known and null means unavailable metadata.</param>
    /// <param name="fusedCandidateCount">The authoritative pre-limit fused count; zero is known and null means unavailable metadata.</param>
    /// <param name="contextExcludedResults">Accepted results excluded from the assembled model context.</param>
    /// <param name="contextBudget">Safe token counts for context assembly.</param>
    /// <param name="reranking">Safe optional reranking execution metadata.</param>
    public RagSearchCompleted(string correlationId, string agentId, string conversationId, string indexName,
        string? originalQuery, string? effectiveQuery, int requestedCandidateCount, int actualCandidateCount,
        int acceptedCount, int rejectedCount, IReadOnlyList<RagSearchSelectedResult> selectedResults,
        IReadOnlyList<RagSearchRejectedResult> rejectedResults, int maximumAcceptedResultCount, TimeSpan duration,
        double? topRawScore, double? topNormalizedRelevance, RagNoContextReason? noContextReason,
        RagIndexReadiness? indexReadiness = null, string? safeFailureSummary = null,
        RagRetrievalMode retrievalMode = RagRetrievalMode.Semantic,
        int? semanticCandidateCount = null, int? lexicalCandidateCount = null, int? fusedCandidateCount = null,
        IReadOnlyList<RagSearchContextExcludedResult>? contextExcludedResults = null,
        RagContextBudgetMetadata? contextBudget = null,
        RagRerankingMetadata? reranking = null)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        ActualCandidateCount = RequireNonNegative(actualCandidateCount, nameof(actualCandidateCount));
        AcceptedCount = RequireNonNegative(acceptedCount, nameof(acceptedCount));
        RejectedCount = RequireNonNegative(rejectedCount, nameof(rejectedCount));
        SelectedResults = selectedResults?.ToArray() ?? throw new ArgumentNullException(nameof(selectedResults));
        RejectedResults = rejectedResults?.ToArray() ?? throw new ArgumentNullException(nameof(rejectedResults));
        ContextExcludedResults = contextExcludedResults?.ToArray() ?? [];
        ContextBudget = contextBudget;
        Reranking = reranking;
        MaximumAcceptedResultCount = RequirePositive(maximumAcceptedResultCount, nameof(maximumAcceptedResultCount));
        Duration = duration < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(duration)) : duration;
        ValidateCounts();
        ValidateOutcome(noContextReason);
        ValidateScores(topRawScore, topNormalizedRelevance);
        TopRawScore = topRawScore;
        TopNormalizedRelevance = topNormalizedRelevance;
        NoContextReason = noContextReason;
        if (indexReadiness is not null && indexReadiness != RagIndexReadiness.Degraded)
            throw new ArgumentOutOfRangeException(nameof(indexReadiness), "Only degraded readiness can be attached to a completed retrieval.");
        IndexReadiness = indexReadiness;
        if (indexReadiness != RagIndexReadiness.Degraded && !string.IsNullOrWhiteSpace(safeFailureSummary))
            throw new ArgumentException("A safe failure summary requires degraded readiness.", nameof(safeFailureSummary));
        var normalizedFailure = string.IsNullOrWhiteSpace(safeFailureSummary) ? null : safeFailureSummary.Trim();
        SafeFailureSummary = normalizedFailure is { Length: > 256 } ? normalizedFailure[..256] : normalizedFailure;
        if (!Enum.IsDefined(retrievalMode)) throw new ArgumentOutOfRangeException(nameof(retrievalMode));
        RetrievalMode = retrievalMode;
        SemanticCandidateCount = RequireOptionalNonNegative(semanticCandidateCount, nameof(semanticCandidateCount));
        LexicalCandidateCount = RequireOptionalNonNegative(lexicalCandidateCount, nameof(lexicalCandidateCount));
        FusedCandidateCount = RequireOptionalNonNegative(fusedCandidateCount, nameof(fusedCandidateCount));
    }

    /// <summary>Gets the number of candidates returned by retrieval.</summary>
    public int ActualCandidateCount { get; }
    /// <summary>Gets the number of candidates accepted as context.</summary>
    public int AcceptedCount { get; }
    /// <summary>Gets the number of candidates rejected by runtime policy.</summary>
    public int RejectedCount { get; }
    /// <summary>Gets the configured maximum number of accepted results.</summary>
    public int MaximumAcceptedResultCount { get; }
    /// <summary>Gets the elapsed retrieval duration.</summary>
    public TimeSpan Duration { get; }
    /// <summary>Gets the finite raw score of the highest-ranked valid candidate, when available.</summary>
    public double? TopRawScore { get; }
    /// <summary>Gets the normalized relevance of the highest-ranked candidate, when reliably available.</summary>
    public double? TopNormalizedRelevance { get; }
    /// <summary>Gets the document and chunk pairs selected as context.</summary>
    public IReadOnlyList<RagSearchSelectedResult> SelectedResults { get; }
    /// <summary>Gets the structured candidates rejected by runtime policy.</summary>
    public IReadOnlyList<RagSearchRejectedResult> RejectedResults { get; }
    /// <summary>Gets the verifiable reason no context was accepted, or null when context was accepted.</summary>
    public RagNoContextReason? NoContextReason { get; }
    /// <summary>Gets degraded readiness when retrieval continued on a usable previous index state.</summary>
    public RagIndexReadiness? IndexReadiness { get; }
    /// <summary>Gets the bounded safe ingestion failure summary associated with degraded readiness.</summary>
    public string? SafeFailureSummary { get; }
    /// <summary>Gets the effective retrieval mode.</summary>
    public RagRetrievalMode RetrievalMode { get; }
    /// <summary>Gets the authoritative semantic source count; zero is known and null means unavailable metadata.</summary>
    public int? SemanticCandidateCount { get; }
    /// <summary>Gets the authoritative lexical source count; zero is known and null means unavailable metadata.</summary>
    public int? LexicalCandidateCount { get; }
    /// <summary>Gets the authoritative pre-limit fused count; zero is known and null means unavailable metadata.</summary>
    public int? FusedCandidateCount { get; }
    /// <summary>Gets accepted results excluded from model context by deterministic selection rules.</summary>
    public IReadOnlyList<RagSearchContextExcludedResult> ContextExcludedResults { get; }
    /// <summary>Gets safe token-budget counts for context assembly.</summary>
    public RagContextBudgetMetadata? ContextBudget { get; }
    /// <summary>Gets safe optional reranking execution metadata.</summary>
    public RagRerankingMetadata? Reranking { get; }

    private static int? RequireOptionalNonNegative(int? value, string parameterName) =>
        value is < 0 ? throw new ArgumentOutOfRangeException(parameterName, value, "The count cannot be negative.") : value;

    private void ValidateCounts()
    {
        if (AcceptedCount + RejectedCount != ActualCandidateCount)
            throw new ArgumentException("Accepted and rejected counts must equal the actual candidate count.");
        if (RejectedResults.Count != RejectedCount)
            throw new ArgumentException("Rejected result count must equal the rejected count.", nameof(RejectedResults));
        if (SelectedResults.Count + ContextExcludedResults.Count != AcceptedCount)
            throw new ArgumentException("Selected and context-excluded result counts must equal the accepted count.");
        if (AcceptedCount > MaximumAcceptedResultCount)
            throw new ArgumentOutOfRangeException(nameof(AcceptedCount), AcceptedCount, "Accepted count cannot exceed the configured maximum.");
    }

    private void ValidateOutcome(RagNoContextReason? noContextReason)
    {
        if (SelectedResults.Count > 0 && noContextReason is not null)
            throw new ArgumentException("A no-context reason cannot be supplied when model context was selected.", nameof(noContextReason));
        if (SelectedResults.Count == 0 && noContextReason is null)
            throw new ArgumentNullException(nameof(noContextReason), "A successful retrieval without selected model context requires a no-context reason.");
        if (noContextReason is not null && !Enum.IsDefined(noContextReason.Value))
            throw new ArgumentOutOfRangeException(nameof(noContextReason), noContextReason, "The no-context reason is not defined.");
    }

    private void ValidateScores(double? topRawScore, double? topNormalizedRelevance)
    {
        if (ActualCandidateCount == 0 && (topRawScore is not null || topNormalizedRelevance is not null))
            throw new ArgumentException("Top scores cannot be supplied when retrieval returned no candidates.");
        if (topRawScore is not null && !double.IsFinite(topRawScore.Value))
            throw new ArgumentOutOfRangeException(nameof(topRawScore), topRawScore, "The top raw score must be finite.");
        if (topNormalizedRelevance is not null && (!double.IsFinite(topNormalizedRelevance.Value) || topNormalizedRelevance.Value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(topNormalizedRelevance), topNormalizedRelevance, "Normalized relevance must be between zero and one.");
    }
}

/// <summary>Indicates that a RAG retrieval operation failed before producing a successful outcome.</summary>
public sealed class RagSearchFailed : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval failed payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    /// <param name="failureClassification">The provider-independent retrieval failure classification.</param>
    /// <param name="duration">The elapsed duration before retrieval failed.</param>
    public RagSearchFailed(string correlationId, string agentId, string conversationId, string indexName,
        string? originalQuery, string? effectiveQuery, int requestedCandidateCount,
        RetrievalErrorCode failureClassification, TimeSpan duration)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        if (!Enum.IsDefined(failureClassification) || failureClassification == RetrievalErrorCode.None)
            throw new ArgumentOutOfRangeException(nameof(failureClassification), failureClassification, "A failed payload requires a failure classification.");
        FailureClassification = failureClassification;
        Duration = duration < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(duration)) : duration;
    }
    /// <summary>Gets the provider-independent retrieval failure classification.</summary>
    public RetrievalErrorCode FailureClassification { get; }
    /// <summary>Gets the elapsed duration before retrieval failed.</summary>
    public TimeSpan Duration { get; }
}
