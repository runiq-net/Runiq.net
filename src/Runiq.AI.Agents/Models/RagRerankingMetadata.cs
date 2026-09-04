using System.Text.Json.Serialization;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Agents;

/// <summary>Describes the observable outcome of optional second-stage reranking.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RagRerankingOutcome
{
    /// <summary>Reranking was disabled.</summary>
    Disabled = 0,
    /// <summary>Reranking completed with a valid complete result.</summary>
    Succeeded = 1,
    /// <summary>The configured fallback preserved original accepted retrieval order after a failure.</summary>
    Fallback = 2,
    /// <summary>Reranking failed and execution was blocked.</summary>
    Failed = 3,
}

/// <summary>Provides safe score, rank, and answerability metadata for one reranked candidate.</summary>
/// <param name="DocumentId">The stable document identifier.</param>
/// <param name="ChunkId">The stable chunk identifier.</param>
/// <param name="OriginalRank">The candidate rank before reranking.</param>
/// <param name="RerankRank">The candidate rank after reranking.</param>
/// <param name="RerankRelevance">The normalized relevance score assigned by the reranker.</param>
/// <param name="Answerability">The answerability classification assigned to the candidate.</param>
public sealed record RagRerankedCandidateMetadata(
    string DocumentId,
    string ChunkId,
    int OriginalRank,
    int RerankRank,
    double RerankRelevance,
    RagAnswerability Answerability);

/// <summary>Provides safe runtime metadata for one optional reranking stage.</summary>
public sealed class RagRerankingMetadata
{
    internal RagRerankingMetadata(
        bool requested,
        bool ran,
        int candidateCount,
        TimeSpan duration,
        RagRerankingOutcome outcome,
        RagRerankerFailurePolicy failurePolicy,
        RagAnswerability answerability,
        IReadOnlyList<RagRerankedCandidateMetadata>? candidates = null,
        bool timedOut = false,
        string? failureCode = null)
    {
        Requested = requested;
        Ran = ran;
        CandidateCount = candidateCount;
        Duration = duration;
        Outcome = outcome;
        FailurePolicy = failurePolicy;
        Answerability = answerability;
        Candidates = candidates?.ToArray() ?? [];
        TimedOut = timedOut;
        FailureCode = failureCode;
    }

    /// <summary>Gets a value indicating whether configuration requested reranking.</summary>
    public bool Requested { get; }
    /// <summary>Gets a value indicating whether a reranker invocation began.</summary>
    public bool Ran { get; }
    /// <summary>Gets the bounded candidate count supplied to the reranker.</summary>
    public int CandidateCount { get; }
    /// <summary>Gets the reranking duration.</summary>
    public TimeSpan Duration { get; }
    /// <summary>Gets the structured stage outcome.</summary>
    public RagRerankingOutcome Outcome { get; }
    /// <summary>Gets the configured failure policy.</summary>
    public RagRerankerFailurePolicy FailurePolicy { get; }
    /// <summary>Gets the aggregate answerability signal.</summary>
    public RagAnswerability Answerability { get; }
    /// <summary>Gets safe per-candidate rerank score, rank, and answerability metadata.</summary>
    public IReadOnlyList<RagRerankedCandidateMetadata> Candidates { get; }
    /// <summary>Gets a value indicating whether the reranker exceeded its configured timeout.</summary>
    public bool TimedOut { get; }
    /// <summary>Gets a safe framework failure classification without provider diagnostics.</summary>
    public string? FailureCode { get; }
}
