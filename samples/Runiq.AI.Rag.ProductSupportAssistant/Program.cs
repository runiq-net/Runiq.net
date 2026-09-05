using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Core;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.ProductSupportAssistant.Agents;

var builder = WebApplication.CreateBuilder(args);
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddRuniqRag(rag => rag.AddIndex(ProductSupportAgent.IndexName, index => index
    .UseDirectory(Path.Combine(AppContext.BaseDirectory, "ProductSupportDocuments"))
    .UseOpenAiEmbeddingModel(OpenAiEmbeddingModels.TextEmbedding3Small)
    .UseInMemoryVectorStore()
    .ConfigureChunking(maxChunkLength: 900, chunkOverlap: 120)
    .ConfigureIngestion(ingestion => ingestion.OnStartup())));

builder.Services.AddRuniqServer(runiq => runiq.AddAgent(
    ProductSupportAgent.Create(openAiApiKey)));

var app = builder.Build();
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq.Net RAG Sample";
    options.Authentication(authentication => authentication.AllowAnonymous());
});
app.Run();
