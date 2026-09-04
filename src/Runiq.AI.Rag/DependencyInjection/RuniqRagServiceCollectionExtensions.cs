using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Chunking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Retrieval;
using Runiq.AI.Rag.Services;
using Runiq.AI.Rag.Telemetry;
using Runiq.AI.Rag.Tools;
using Runiq.AI.Rag.VectorStores;
using Runiq.AI.Rag.VectorStores.InMemory;
using Runiq.AI.Rag.Ingestion;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Runtime;
using Runiq.AI.Rag.Hosting;
using Runiq.AI.Rag.Controllers;

namespace Runiq.AI.Rag.DependencyInjection;

/// <summary>
/// Provides dependency injection registration methods for RAG services.
/// </summary>
public static class RuniqRagServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default RAG services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRagEmbeddingInputPreparer, DefaultRagEmbeddingInputPreparer>();
        services.TryAddScoped<IRagChunkEmbeddingGenerator, DefaultRagChunkEmbeddingGenerator>();
        services.TryAddScoped<IRagVectorRecordMapper, DefaultRagVectorRecordMapper>();
        services.TryAddScoped<IRagUpsertVectorRequestMapper, DefaultRagUpsertVectorRequestMapper>();
        services.TryAddSingleton<IRagVectorRecordDimensionValidator, DefaultRagVectorRecordDimensionValidator>();
        services.TryAddDefaultRagVectorStore();
        services.TryAddScoped<IRagVectorStoreUpsertPipeline, DefaultRagVectorStoreUpsertPipeline>();
        services.TryAddSingleton<IRagChunker, DefaultRagChunker>();
        services.TryAddScoped<IRagRetriever, DefaultRetriever>();
        services.TryAddScoped<IRagRetrievalPipeline, DefaultRagRetrievalPipeline>();
        services.TryAddScoped<IVectorQueryTool, DefaultVectorQueryTool>();
        services.TryAddScoped<IRagService, RagService>();
        services.TryAddSingleton<RagIngestionState>();
        services.Configure<RagIngestionOptions>(_ => { });
        services.AddOptions<RagObservabilityOptions>()
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RagObservabilityOptions>, RagObservabilityOptionsValidator>());
        services.TryAddScoped<IRagDocumentIngestionService, DefaultRagDocumentIngestionService>();
        services.TryAddSingleton<RagIngestionManager>();
        services.TryAddSingleton<IRagIngestionManager>(provider => provider.GetRequiredService<RagIngestionManager>());
        services.TryAddSingleton<RagRuntimeProviderRegistry>();
        services.TryAddScoped<IRagIndexRuntimeConfigurationResolver, RagIndexRuntimeConfigurationResolver>();

        // Last-operation telemetry: singleton so snapshots survive scoped pipeline instances, registered
        // through TryAdd so hosts can replace the recorder, the reader, or the time source.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DefaultRagOperationTelemetryRecorder>();
        services.TryAddSingleton<IRagOperationTelemetryRecorder>(
            provider => provider.GetRequiredService<DefaultRagOperationTelemetryRecorder>());
        services.TryAddSingleton<IRagOperationTelemetryReader>(
            provider => provider.GetRequiredService<DefaultRagOperationTelemetryRecorder>());

        services.Configure<RagOptions>(_ => { });
        services.TryAddSingleton<IRagIndexRegistry>(new RagIndexRegistry(Array.Empty<RagIndexRegistration>()));
        services.AddMvcCore()
            .AddApplicationPart(typeof(RagManagementController).Assembly);

        return services;
    }

    /// <summary>
    /// Registers the Core embedding client used by RAG document and query pipelines.
    /// </summary>
    /// <typeparam name="TClient">The Core embedding client implementation type.</typeparam>
    /// <param name="services">The service collection to add the embedding client to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagEmbeddingClient<TClient>(this IServiceCollection services)
        where TClient : class, IEmbeddingClient
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IEmbeddingClient, TClient>());
        GetRuntimeProviders(services).Embeddings["default"] = provider => provider.GetRequiredService<IEmbeddingClient>();

        return services;
    }

    /// <summary>
    /// Registers the Core embedding client used by RAG using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the embedding client to.</param>
    /// <param name="factory">The factory used to create the embedding client.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagEmbeddingClient(
        this IServiceCollection services,
        Func<IServiceProvider, IEmbeddingClient> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.Replace(ServiceDescriptor.Singleton(factory));
        GetRuntimeProviders(services).Embeddings["default"] = provider => provider.GetRequiredService<IEmbeddingClient>();

        return services;
    }

    /// <summary>Registers an embedding client factory for an effective index embedding reference.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="reference">The exact provider/model or custom embedding reference.</param>
    /// <param name="factory">The scoped-resolution-safe client factory.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRagEmbeddingClient(this IServiceCollection services, string reference, Func<IServiceProvider, IEmbeddingClient> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        var normalized = RequireRuntimeReference(reference, nameof(reference));
        GetRuntimeProviders(services).Embeddings[normalized] = factory;
        services.TryAddScoped<IEmbeddingClient>(factory);
        return services;
    }

    /// <summary>
    /// Registers the specified RAG chunker implementation.
    /// </summary>
    /// <typeparam name="TChunker">The provider-neutral chunker implementation type.</typeparam>
    /// <param name="services">The service collection to add the chunker to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagChunker<TChunker>(this IServiceCollection services)
        where TChunker : class, IRagChunker
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IRagChunker, TChunker>());

        return services;
    }

    /// <summary>
    /// Registers a RAG chunker using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the chunker to.</param>
    /// <param name="factory">The factory used to create the chunker.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagChunker(
        this IServiceCollection services,
        Func<IServiceProvider, IRagChunker> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.Replace(ServiceDescriptor.Singleton(factory));

        return services;
    }

    /// <summary>
    /// Registers the in-memory RAG vector store.
    /// </summary>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddInMemoryRagVectorStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<InMemoryRagVectorStore>();
        services.ReplaceRagVectorStore(ServiceDescriptor.Singleton<IRagVectorStore>(provider => provider.GetRequiredService<InMemoryRagVectorStore>()));
        GetRuntimeProviders(services).VectorStores["in-memory"] = provider => provider.GetRequiredService<InMemoryRagVectorStore>();
        GetRuntimeProviders(services).VectorStores["default"] = provider => provider.GetRequiredService<IRagVectorStore>();
        return services;
    }

    /// <summary>
    /// Registers the specified RAG vector store implementation.
    /// </summary>
    /// <typeparam name="TVectorStore">The vector store implementation type.</typeparam>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagVectorStore<TVectorStore>(this IServiceCollection services)
        where TVectorStore : class, IRagVectorStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ReplaceRagVectorStore(ServiceDescriptor.Singleton<IRagVectorStore, TVectorStore>());
        GetRuntimeProviders(services).VectorStores["default"] = provider => provider.GetRequiredService<IRagVectorStore>();

        return services;
    }

    /// <summary>
    /// Registers a RAG vector store using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <param name="factory">The factory used to create the vector store.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, IRagVectorStore> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.ReplaceRagVectorStore(ServiceDescriptor.Singleton(factory));
        GetRuntimeProviders(services).VectorStores["default"] = provider => provider.GetRequiredService<IRagVectorStore>();

        return services;
    }

    /// <summary>Registers a vector-store factory for an effective index store reference.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="reference">The exact custom or named vector-store reference.</param>
    /// <param name="factory">The scoped-resolution-safe store factory.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRagVectorStore(this IServiceCollection services, string reference, Func<IServiceProvider, IRagVectorStore> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        var normalized = RequireRuntimeReference(reference, nameof(reference));
        GetRuntimeProviders(services).VectorStores[normalized] = factory;
        return services;
    }

    /// <summary>
    /// Adds the default RAG services to the dependency injection container and applies fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configure">The fluent configuration action to apply.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        Action<RagBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddRuniqRag();

        var builder = new RagBuilder(services);
        configure(builder);

        return services;
    }

    /// <summary>
    /// Adds the default RAG services to the dependency injection container and binds RAG options from configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configuration">The configuration source that contains RAG options.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRuniqRag();
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Adds the default RAG services, binds RAG options from configuration, and applies fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configuration">The configuration source that contains RAG options.</param>
    /// <param name="configure">The fluent configuration action to apply.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RagBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddRuniqRag(configuration);

        var builder = new RagBuilder(services);
        configure(builder);

        return services;
    }

    private static void TryAddDefaultRagVectorStore(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RagVectorStoreProviderRegistration)))
        {
            return;
        }

        var existingVectorStoreDescriptor = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IRagVectorStore));

        if (existingVectorStoreDescriptor is null)
        {
            services.ReplaceRagVectorStore(ServiceDescriptor.Singleton<IRagVectorStore, NullVectorStore>());
            return;
        }

        services.ReplaceRagVectorStore(existingVectorStoreDescriptor);
    }

    private static void ReplaceRagVectorStore(
        this IServiceCollection services,
        ServiceDescriptor vectorStoreDescriptor)
    {
        services.TryAddSingleton<IRagVectorRecordDimensionValidator, DefaultRagVectorRecordDimensionValidator>();
        services.RemoveAll<IRagVectorStore>();
        services.RemoveAll<RagVectorStoreProviderRegistration>();

        services.Add(ServiceDescriptor.Describe(
            typeof(RagVectorStoreProviderRegistration),
            serviceProvider => new RagVectorStoreProviderRegistration(
                CreateVectorStore(vectorStoreDescriptor, serviceProvider)),
            vectorStoreDescriptor.Lifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IRagVectorStore),
            serviceProvider => new ValidatingRagVectorStore(
                serviceProvider.GetRequiredService<RagVectorStoreProviderRegistration>().VectorStore,
                serviceProvider.GetRequiredService<IRagVectorRecordDimensionValidator>()),
            vectorStoreDescriptor.Lifetime));
    }

    private static IRagVectorStore CreateVectorStore(
        ServiceDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        if (descriptor.ImplementationInstance is IRagVectorStore instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IRagVectorStore)descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IRagVectorStore)ActivatorUtilities.CreateInstance(
                serviceProvider,
                descriptor.ImplementationType);
        }

        throw new InvalidOperationException("The RAG vector store registration is invalid.");
    }

    private static RagRuntimeProviderRegistry GetRuntimeProviders(IServiceCollection services)
    {
        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(RagRuntimeProviderRegistry))?.ImplementationInstance as RagRuntimeProviderRegistry;
        if (existing is not null) return existing;
        var registry = new RagRuntimeProviderRegistry();
        services.RemoveAll<RagRuntimeProviderRegistry>();
        services.AddSingleton(registry);
        return registry;
    }

    private static string RequireRuntimeReference(string reference, string parameterName) =>
        string.IsNullOrWhiteSpace(reference) ? throw new ArgumentException("A non-empty runtime reference is required.", parameterName) : reference.Trim();
}

