using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents;

/// <summary>
/// Describes the framework-observed RAG policy outcome for an agent execution.
/// </summary>
public sealed class AgentRagExecutionMetadata
{
    internal AgentRagExecutionMetadata(
        RagExecutionMode mode,
        bool hasAcceptedContext,
        RagNoContextBehavior? appliedNoContextBehavior,
        RagNoContextReason? noContextReason,
        bool modelInvocationSkipped,
        bool isAnswerGrounded,
        IReadOnlyList<RagSearchResult> candidates,
        IReadOnlyList<RagSearchResult> acceptedResults,
        IReadOnlyList<RagRejectedResult> rejectedResults,
        RagRetrievalMode retrievalMode,
        RagRetrievalStatistics retrievalStatistics,
        IReadOnlyList<RagSearchResult>? contextSelectedResults = null,
        IReadOnlyList<RagContextExcludedResult>? contextExcludedResults = null,
        RagContextBudgetMetadata? contextBudget = null,
        RagRerankingMetadata? reranking = null)
    {
        Mode = mode;
        HasAcceptedContext = hasAcceptedContext;
        AppliedNoContextBehavior = appliedNoContextBehavior;
        NoContextReason = noContextReason;
        ModelInvocationSkipped = modelInvocationSkipped;
        IsAnswerGrounded = isAnswerGrounded;
        Candidates = candidates;
        AcceptedResults = acceptedResults;
        RejectedResults = rejectedResults;
        RetrievalMode = retrievalMode;
        SemanticCandidateCount = retrievalStatistics.SemanticCandidateCount;
        LexicalCandidateCount = retrievalStatistics.LexicalCandidateCount;
        FusedCandidateCount = retrievalStatistics.FusedCandidateCount;
        ContextSelectedResults = contextSelectedResults ?? acceptedResults;
        ContextExcludedResults = contextExcludedResults ?? [];
        ContextBudget = contextBudget;
        Reranking = reranking;
    }

    /// <summary>
    /// Gets the RAG execution mode applied by the framework.
    /// </summary>
    public RagExecutionMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether retrieval produced context accepted by the runtime policy.
    /// </summary>
    public bool HasAcceptedContext { get; }

    /// <summary>
    /// Gets the no-context behavior applied by the framework, or null when accepted context was available.
    /// </summary>
    public RagNoContextBehavior? AppliedNoContextBehavior { get; }

    /// <summary>
    /// Gets the verifiable no-context reason, or null when context was accepted or retrieval failed.
    /// </summary>
    public RagNoContextReason? NoContextReason { get; }

    /// <summary>
    /// Gets a value indicating whether the framework skipped model/provider invocation.
    /// </summary>
    public bool ModelInvocationSkipped { get; }

    /// <summary>
    /// Gets a value indicating whether the framework constrained the final model response to accepted context.
    /// This reports the applied policy and does not independently verify model semantics.
    /// </summary>
    public bool IsAnswerGrounded { get; }

    /// <summary>
    /// Gets every raw retrieval candidate after framework-known relevance normalization and deterministic ordering.
    /// </summary>
    public IReadOnlyList<RagSearchResult> Candidates { get; }

    /// <summary>
    /// Gets the candidates accepted as Agent Chat context after all acceptance rules and result limits were applied.
    /// </summary>
    public IReadOnlyList<RagSearchResult> AcceptedResults { get; }

    /// <summary>
    /// Gets the candidates excluded from Agent Chat context together with their explicit rejection reasons.
    /// </summary>
    public IReadOnlyList<RagRejectedResult> RejectedResults { get; }

    /// <summary>
    /// Gets the number of raw retrieval candidates.
    /// </summary>
    public int CandidateCount => Candidates.Count;

    /// <summary>
    /// Gets the number of candidates accepted as Agent Chat context.
    /// </summary>
    public int AcceptedCount => AcceptedResults.Count;

    /// <summary>
    /// Gets the number of rejected retrieval candidates.
    /// </summary>
    public int RejectedCount => RejectedResults.Count;

    /// <summary>Gets the accepted retrieval results selected into the final model context.</summary>
    public IReadOnlyList<RagSearchResult> ContextSelectedResults { get; }

    /// <summary>Gets accepted retrieval results excluded during deterministic context selection.</summary>
    public IReadOnlyList<RagContextExcludedResult> ContextExcludedResults { get; }

    /// <summary>Gets safe token-budget counts for context assembly, or null when assembly did not run.</summary>
    public RagContextBudgetMetadata? ContextBudget { get; }

    /// <summary>Gets the number of accepted results selected into model context.</summary>
    public int ContextSelectedCount => ContextSelectedResults.Count;

    /// <summary>Gets the number of accepted results excluded from model context.</summary>
    public int ContextExcludedCount => ContextExcludedResults.Count;

    /// <summary>Gets the optional reranking execution metadata.</summary>
    public RagRerankingMetadata? Reranking { get; }

    /// <summary>Gets the effective retrieval mode.</summary>
    public RagRetrievalMode RetrievalMode { get; }

    /// <summary>Gets the authoritative semantic source count, where zero is known and null means it was not provided.</summary>
    public int? SemanticCandidateCount { get; }

    /// <summary>Gets the authoritative lexical source count, where zero is known and null means it was not provided.</summary>
    public int? LexicalCandidateCount { get; }

    /// <summary>Gets the authoritative pre-limit fused count, where zero is known and null means it was not provided.</summary>
    public int? FusedCandidateCount { get; }
}
