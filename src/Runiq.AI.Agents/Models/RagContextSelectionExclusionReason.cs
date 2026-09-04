namespace Runiq.AI.Agents;

/// <summary>
/// Describes why an accepted retrieval result was excluded from the assembled model context.
/// </summary>
public enum RagContextSelectionExclusionReason
{
    /// <summary>The complete chunk did not fit in the remaining RAG context token budget.</summary>
    TokenBudgetExceeded = 0,

    /// <summary>The chunk materially overlapped a higher-priority selected chunk from the same document.</summary>
    OverlappingContent = 1,

    /// <summary>The configured maximum selected chunks for the source was reached.</summary>
    SourceLimitExceeded = 2,

    /// <summary>The deterministic diversity pass deferred the chunk and it could not subsequently be selected.</summary>
    SourceDiversityPreference = 3,

    /// <summary>The structured reranker answerability decision did not permit grounded model context.</summary>
    NotAnswerable = 4,
}
