using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Bir agent calistirmasi sirasinda cozumlenen RAG context bilgilerini temsil eder.
/// </summary>
public sealed record AgentRuntimeContext(
    IReadOnlyList<RagSearchResult>? RagSearchResults = null)
{
    internal AgentRuntimeContext(
        IReadOnlyList<RagSearchResult> acceptedResults,
        IReadOnlyList<RagSearchResult> candidates,
        IReadOnlyList<RagRejectedResult> rejectedResults,
        Configuration.RagNoContextReason? noContextReason,
        Runiq.AI.Rag.Models.Retrieval.RagRetrievalStatistics? retrievalStatistics = null,
        IReadOnlyList<RagSearchResult>? acceptedResultsBeforeSelection = null,
        IReadOnlyList<RagContextExcludedResult>? contextExcludedResults = null,
        RagContextBudgetMetadata? contextBudget = null,
        RagRerankingMetadata? reranking = null)
        : this(acceptedResults)
    {
        RetrievedRagCandidates = candidates;
        RejectedRagCandidates = rejectedResults;
        NoContextReason = noContextReason;
        RetrievalStatistics = retrievalStatistics ?? Runiq.AI.Rag.Models.Retrieval.RagRetrievalStatistics.Empty;
        AcceptedRagResults = acceptedResultsBeforeSelection ?? acceptedResults;
        ContextExcludedResults = contextExcludedResults ?? [];
        ContextBudget = contextBudget;
        Reranking = reranking;
    }

    /// <summary>
    /// Calistirma sirasinda kullanici girdisine gore bulunan RAG arama sonuclarini doner.
    /// </summary>
    public IReadOnlyList<RagSearchResult> RetrievedRagContext =>
        RagSearchResults ?? [];

    /// <summary>
    /// Gets the raw retrieval candidates before the runtime applies context acceptance criteria.
    /// </summary>
    internal IReadOnlyList<RagSearchResult> RetrievedRagCandidates { get; } =
        RagSearchResults ?? [];

    internal IReadOnlyList<RagRejectedResult> RejectedRagCandidates { get; } = [];
    internal IReadOnlyList<RagSearchResult> AcceptedRagResults { get; } = RagSearchResults ?? [];
    internal IReadOnlyList<RagContextExcludedResult> ContextExcludedResults { get; } = [];
    internal RagContextBudgetMetadata? ContextBudget { get; }
    internal RagRerankingMetadata? Reranking { get; }

    internal Configuration.RagNoContextReason? NoContextReason { get; }

    internal Runiq.AI.Rag.Models.Retrieval.RagRetrievalStatistics RetrievalStatistics { get; } =
        Runiq.AI.Rag.Models.Retrieval.RagRetrievalStatistics.Empty;

    /// <summary>
    /// Identifies the single retrieval lifecycle supported by one agent execution.
    /// </summary>
    internal string? RetrievalCorrelationId { get; init; }

    /// <summary>
    /// Calistirma icin herhangi bir context bilgisinin cozulup cozulmedigini belirtir.
    /// </summary>
    public bool HasContext => RetrievedRagContext.Count > 0;
}
