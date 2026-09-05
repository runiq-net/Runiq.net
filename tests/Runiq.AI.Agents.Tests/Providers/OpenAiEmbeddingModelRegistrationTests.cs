using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Agents;
using Runiq.AI.Core;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class OpenAiEmbeddingModelRegistrationTests
{
    // Verifies that the typed small OpenAI model resolves to its provider-visible effective reference.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldResolveSmallModel()
    {
        var registration = Register(OpenAiEmbeddingModels.TextEmbedding3Small);

        Assert.Equal("openai/text-embedding-3-small", registration.EmbeddingReference);
        Assert.Equal("OpenAI text-embedding-3-small", registration.EmbeddingDisplayName);
    }

    // Verifies that the typed large OpenAI model resolves to its provider-visible effective reference.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldResolveLargeModel() =>
        Assert.Equal("openai/text-embedding-3-large", Register(OpenAiEmbeddingModels.TextEmbedding3Large).EmbeddingReference);

    // Verifies that the OpenAI convenience method rejects a typed reference owned by another provider.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldRejectOtherProvider() =>
        Assert.Throws<ArgumentException>(() => Register(new RagEmbeddingModelReference("custom", "model", "Custom")));

    // Verifies the framework OpenAI adapter maps ordered provider vectors to the shared embedding contract.
    [Fact]
    public async Task OpenAIEmbeddingClient_ShouldMapOrderedEmbeddingResults()
    {
        var handler = new EmbeddingHandler("""
            {"data":[{"index":1,"embedding":[0.3,0.4]},{"index":0,"embedding":[0.1,0.2]}]}
            """);
        var client = new OpenAIEmbeddingClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test/v1/") },
            "test-key");

        var result = await client.EmbedAsync(new EmbeddingRequest(
            ModelReference.Parse("openai/text-embedding-3-small"),
            ["first", "second"]));

        Assert.Equal([0.1f, 0.2f], result.Results[0].Vector);
        Assert.Equal([0.3f, 0.4f], result.Results[1].Vector);
        Assert.Equal("Bearer test-key", handler.Authorization);
    }

    // Verifies an OpenAI RAG agent automatically supplies the framework embedding client without sample plumbing.
    [Fact]
    public void AddRuniqServer_ShouldRegisterOpenAIEmbeddingClient_ForAttachedRagIndex()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
            .UseDirectory("documents")
            .UseOpenAiEmbeddingModel(OpenAiEmbeddingModels.TextEmbedding3Small)
            .UseInMemoryVectorStore()));
        services.AddRuniqServer(options => options.AddAgent(
            new Agent("rag-agent", "RAG Agent", "Answer from context.", "openai/gpt-5", "test-key")
                .UseRag(rag => rag.IndexName = "documents")));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<OpenAIEmbeddingClient>(provider.GetRequiredService<IEmbeddingClient>());
    }

    private static RagIndexRegistration Register(RagEmbeddingModelReference model)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
            .UseDirectory("documents")
            .UseVectorStore("store")
            .UseOpenAiEmbeddingModel(model)));
        using var provider = services.BuildServiceProvider();
        return Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
    }

    private sealed class EmbeddingHandler(string response) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}
