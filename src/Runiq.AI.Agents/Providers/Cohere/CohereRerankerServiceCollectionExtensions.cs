using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Reranking;

namespace Runiq.AI.Agents.Providers.Cohere;

/// <summary>Registers the supported Cohere reranking integration.</summary>
public static class CohereRerankerServiceCollectionExtensions
{
    /// <summary>Registers Cohere Rerank v2 as the runtime reranker.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures credentials, model, and answerability threshold.</param>
    /// <returns>The same service collection for fluent composition.</returns>
    public static IServiceCollection AddCohereReranker(
        this IServiceCollection services,
        Action<CohereRerankerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CohereRerankerOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "A Cohere API key is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "A Cohere rerank model is required.")
            .Validate(options => double.IsFinite(options.MinimumAnswerableRelevance) &&
                                 options.MinimumAnswerableRelevance is >= 0 and <= 1,
                "The answerability threshold must be between zero and one.")
            .ValidateOnStart();
        services.AddHttpClient<CohereReranker>();
        services.AddScoped<IRagReranker>(provider => provider.GetRequiredService<CohereReranker>());
        return services;
    }
}
