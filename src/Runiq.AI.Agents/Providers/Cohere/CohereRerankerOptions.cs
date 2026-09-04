namespace Runiq.AI.Agents.Providers.Cohere;

/// <summary>Configures the supported Cohere Rerank v2 integration.</summary>
public sealed class CohereRerankerOptions
{
    /// <summary>Gets or sets the Cohere API key. Supply it from a secret provider or environment variable.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Cohere rerank model identifier.</summary>
    public string Model { get; set; } = "rerank-v4.0-fast";

    /// <summary>Gets or sets the inclusive relevance threshold used to derive candidate and aggregate answerability.</summary>
    public double MinimumAnswerableRelevance { get; set; } = 0.5;

    /// <summary>Gets or sets the optional Cohere client name header.</summary>
    public string ClientName { get; set; } = "Runiq.AI";
}
