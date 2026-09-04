using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Provides agent-level RAG retrieval configuration.
/// </summary>
public sealed class AgentRagOptions
{
    private RagResultAcceptanceOptions acceptance = new();
    private RagContextBudgetOptions contextBudget = new();
    private RagRerankingOptions reranking = new();

    /// <summary>
    /// Gets or sets a value indicating whether agent RAG retrieval is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the vector index name used by agent RAG queries.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets how accepted retrieval context constrains the response. The default is
    /// <see cref="RagExecutionMode.Open"/> to preserve normal agent behavior when no context is accepted.
    /// </summary>
    public RagExecutionMode Mode { get; set; } = RagExecutionMode.Open;

    /// <summary>Gets or sets the retrieval sources used for RAG. The default remains semantic.</summary>
    public RagRetrievalMode RetrievalMode { get; set; } = RagRetrievalMode.Semantic;

    /// <summary>
    /// Gets or sets how a successful retrieval with no accepted context affects execution. The default is
    /// <see cref="RagNoContextBehavior.AnswerNormally"/>.
    /// </summary>
    public RagNoContextBehavior NoContextBehavior { get; set; } = RagNoContextBehavior.AnswerNormally;

    /// <summary>
    /// Gets or sets the result-acceptance policy that separates retrieval candidates from accepted Agent Chat
    /// context. The default requests 20 candidates, accepts at most five, and applies no minimum relevance threshold.
    /// </summary>
    public RagResultAcceptanceOptions Acceptance
    {
        get => acceptance;
        set => acceptance = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the token budget and deterministic source-selection policy applied after retrieval acceptance.
    /// </summary>
    public RagContextBudgetOptions ContextBudget
    {
        get => contextBudget;
        set => contextBudget = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the optional second-stage reranking and answerability policy.</summary>
    public RagRerankingOptions Reranking
    {
        get => reranking;
        set => reranking = value ?? throw new ArgumentNullException(nameof(value));
    }
}

