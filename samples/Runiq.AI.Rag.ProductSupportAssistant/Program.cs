using Runiq.AI.Core;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.ProductSupportAssistant.Agents;
using Runiq.AI.Rag.ProductSupportAssistant.Services;

var builder = WebApplication.CreateBuilder(args);
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddRuniqRag(rag => rag.AddIndex(ProductSupportAgent.IndexName, index => index
    .UseDirectory(Path.Combine(AppContext.BaseDirectory, "ProductSupportDocuments", "corporate"), searchPattern: "*.md")
    .UseEmbeddingModel("ollama/deterministic-sample")
    .UseInMemoryVectorStore()
    .ConfigureChunking(maxChunkLength: 900, chunkOverlap: 120)
    .ConfigureIngestion(ingestion => ingestion.OnStartup())));
builder.Services.AddRagEmbeddingClient(
    "ollama/deterministic-sample",
    _ => new DeterministicSampleEmbeddingClient());

builder.Services.AddRuniqServer(runiq => runiq.AddAgent(
    ProductSupportAgent.Create(openAiApiKey)));

var app = builder.Build();
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq.Net Corporate Document Assistant";
    options.Authentication(authentication => authentication.AllowAnonymous());
});
app.Run();
