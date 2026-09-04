namespace Runiq.AI.Rag.Models.Reranking;

/// <summary>Describes whether the reranked evidence is sufficient to answer the current query.</summary>
public enum RagAnswerability
{
    /// <summary>The reranker could not make a reliable answerability decision.</summary>
    Unknown = 0,
    /// <summary>The accepted evidence is sufficient to answer the query.</summary>
    Answerable = 1,
    /// <summary>The accepted evidence is insufficient to answer the query.</summary>
    NotAnswerable = 2,
}

/// <summary>Provides one bounded candidate to a provider-neutral reranker.</summary>
/// <param name="DocumentId">The stable source document identity.</param>
/// <param name="ChunkId">The stable chunk identity.</param>
/// <param name="Content">The untrusted chunk content used only as reranker input.</param>
/// <param name="OriginalRank">The one-based accepted retrieval rank.</param>
public sealed record RagRerankCandidate(
    string DocumentId,
    string ChunkId,
    string Content,
    int OriginalRank);

/// <summary>Provides a query and bounded accepted candidates to a reranker.</summary>
/// <param name="Query">The current user query.</param>
/// <param name="Candidates">The bounded candidates in original accepted retrieval order.</param>
public sealed record RagRerankRequest(
    string Query,
    IReadOnlyList<RagRerankCandidate> Candidates);

/// <summary>Provides the reranker decision for one requested candidate.</summary>
/// <param name="DocumentId">The stable source document identity.</param>
/// <param name="ChunkId">The stable chunk identity.</param>
/// <param name="Relevance">Provider-neutral relevance in the inclusive range from zero to one; larger is better.</param>
/// <param name="Answerability">The candidate-level answerability signal.</param>
public sealed record RagRerankCandidateResult(
    string DocumentId,
    string ChunkId,
    double Relevance,
    RagAnswerability Answerability = RagAnswerability.Unknown);

/// <summary>Provides complete candidate decisions and aggregate answerability for one reranking request.</summary>
/// <param name="Candidates">One validated decision for every requested candidate.</param>
/// <param name="Answerability">The aggregate answerability of the candidate set.</param>
public sealed record RagRerankResult(
    IReadOnlyList<RagRerankCandidateResult> Candidates,
    RagAnswerability Answerability);
