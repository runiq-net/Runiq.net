namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Describes a framework-verifiable reason why retrieval produced no accepted context.
/// </summary>
public enum RagNoContextReason
{
    /// <summary>
    /// Retrieval completed successfully but returned no candidates. This reason does not distinguish an
    /// empty index from a query that matched no records.
    /// </summary>
    NoResults = 0,

    /// <summary>
    /// Retrieval returned candidates, but none met the configured minimum relevance score.
    /// </summary>
    BelowRelevanceThreshold = 1,

    /// <summary>
    /// Retrieval returned candidates, but every candidate was rejected for one or more explicit acceptance reasons.
    /// </summary>
    CandidatesRejected = 2,

    /// <summary>
    /// Retrieval produced accepted results, but context-selection rules could not select any complete chunk.
    /// </summary>
    ContextBudgetExhausted = 3,

    /// <summary>Reranking completed, but its structured aggregate answerability was not answerable.</summary>
    NotAnswerable = 4,

    /// <summary>Retrieval produced accepted results, but fail-closed reranking prevented context assembly.</summary>
    RerankingFailed = 5,
}
