using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Providers;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Validation;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.Agents;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Tools;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Runiq.AI.Agents.Controllers;
using Runiq.AI.Rag.Abstractions.Observability;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Runtime;
using Runiq.AI.Rag.DependencyInjection;

namespace Runiq.AI.Core;

/// <summary>
/// Registers agent runtime, provider clients, tool execution, and dashboard endpoint services for a Runiq server host.
/// </summary>
public static class RuniqAgentServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core server services plus host-defined agent, tool, and context space definitions.
    /// </summary>
    /// <param name="services">The host application's service collection.</param>
    /// <param name="configure">A callback that adds agents, tools, and context spaces to the server options.</param>
    /// <returns>The same service collection for fluent startup composition.</returns>
    public static IServiceCollection AddRuniqServer(
        this IServiceCollection services,
        Action<RuniqServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqServerOptions();

        configure(options);

        AgentValidator.ValidateRegisteredAgents(options.Agents);


        services.AddSingleton<IReadOnlyList<AgentToolRegistration>>(
            BuildRegisteredToolRegistry(options));

        foreach (var agent in options.Agents)
        {
            services.AddSingleton(agent);
        }

        RegisterOpenAiRagEmbeddingClients(services, options.Agents);

        services.AddRuniqServer();
        services.AddRuniqAgentServer();

        return services;
    }

    private static void RegisterOpenAiRagEmbeddingClients(
        IServiceCollection services,
        IReadOnlyList<Agent> agents)
    {
        var registry = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IRagIndexRegistry))?
            .ImplementationInstance as IRagIndexRegistry;
        if (registry is null)
        {
            return;
        }

        var registrations = agents
            .Where(agent => agent.Rag is not null &&
                string.Equals(agent.ProviderName, "openai", StringComparison.OrdinalIgnoreCase))
            .Select(agent => new
            {
                Agent = agent,
                Index = registry.Registrations.SingleOrDefault(index =>
                    string.Equals(index.Name, agent.Rag!.IndexName, StringComparison.Ordinal))
            })
            .Where(item => item.Index is not null &&
                item.Index.EmbeddingReference.StartsWith("openai/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (registrations.Length == 0)
        {
            return;
        }

        services.AddHttpClient(OpenAIEmbeddingClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        foreach (var registration in registrations)
        {
            services.AddRagEmbeddingClient(
                registration.Index!.EmbeddingReference,
                provider => new OpenAIEmbeddingClient(
                    provider.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(OpenAIEmbeddingClient.HttpClientName),
                    registration.Agent.ApiKey));
        }
    }

    /// <summary>
    /// Registers agent runtime services without registering new agent definitions.
    /// </summary>
    /// <param name="services">The host application's service collection.</param>
    /// <returns>The same service collection for fluent startup composition.</returns>
    public static IServiceCollection AddRuniqAgentServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRuntimeMetadataService, RuntimeMetadataService>();
        services.AddMvcCore()
            .AddApplicationPart(typeof(AgentChatController).Assembly);

        services.AddHttpClient<OpenAIResponsesClient>();
        services.AddHttpClient<OpenAICompatibleClient>();
        services.TryAddSingleton<IModelCapabilityResolver, DefaultModelCapabilityResolver>();
        services.TryAddScoped<IChatClientResolver, ChatClientResolver>();
        services.AddScoped<AgentToolInvoker>();

        services.AddScoped<ToolRunApiHandler>();
        services.AddScoped<RagObservabilityProjection>(provider => new RagObservabilityProjection(
            provider.GetRequiredService<IOptions<RagObservabilityOptions>>(),
            provider.GetService<IRagObservabilityRedactor>(),
            provider.GetService<IRagObservabilityMetadataProjector>(),
            provider.GetRequiredService<ILogger<RagObservabilityProjection>>()));
        services.AddScoped<AgentExecutionRuntime>(provider => new AgentExecutionRuntime(
            provider.GetServices<Agent>(),
            provider.GetRequiredService<IChatClientResolver>(),
            provider.GetRequiredService<AgentToolInvoker>(),
            provider.GetService<IRagRetriever>(),
            provider.GetRequiredService<RagObservabilityProjection>(),
            provider.GetService<IRagIndexRegistry>(),
            provider.GetService<IRagIngestionManager>(),
            provider.GetService<Runiq.AI.Rag.Abstractions.Reranking.IRagReranker>()));
        services.AddScoped<AgentChatApiHandler>();

        return services;
    }

    private static IReadOnlyList<AgentToolRegistration> BuildRegisteredToolRegistry(
        RuniqServerOptions options)
    {
        var toolsByName = new Dictionary<string, AgentToolRegistration>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var tool in options.Tools)
        {
            AddToolRegistration(toolsByName, tool);
        }

        foreach (var tool in options.Agents.SelectMany(agent => agent.Tools))
        {
            AddToolRegistration(toolsByName, tool);
        }

        return toolsByName
            .Values
            .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddToolRegistration(
        Dictionary<string, AgentToolRegistration> toolsByName,
        AgentToolRegistration tool)
    {
        if (!toolsByName.TryGetValue(tool.Name, out var existing))
        {
            toolsByName[tool.Name] = tool;
            return;
        }

        if (existing.ToolType == tool.ToolType)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Tool name '{tool.Name}' is registered by multiple tool types: " +
            $"'{existing.ToolType.FullName}' and '{tool.ToolType.FullName}'. " +
            "Tool names must be unique.");
    }
}
