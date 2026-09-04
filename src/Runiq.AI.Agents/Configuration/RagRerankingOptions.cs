namespace Runiq.AI.Agents.Configuration;

/// <summary>Configures optional second-stage reranking and answerability evaluation.</summary>
public sealed class RagRerankingOptions
{
    /// <summary>Gets or sets a value indicating whether reranking is enabled. The default is false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the maximum accepted candidates sent to the reranker. The default is five.</summary>
    public int MaximumCandidates { get; set; } = 5;

    /// <summary>Gets or sets the maximum reranker duration. The default is five seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the behavior used for timeout, unavailable service, invalid output, or failure.</summary>
    public RagRerankerFailurePolicy FailurePolicy { get; set; } = RagRerankerFailurePolicy.UseOriginalOrder;
}

/// <summary>Describes how runtime execution handles a reranker failure.</summary>
public enum RagRerankerFailurePolicy
{
    /// <summary>Stops RAG execution and prevents model invocation.</summary>
    Fail = 0,
    /// <summary>Continues with the exact accepted retrieval order and records the fallback.</summary>
    UseOriginalOrder = 1,
}
