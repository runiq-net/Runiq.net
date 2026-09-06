# Runiq.Net Corporate Document Assistant

This sample shows a grounded corporate document assistant with Runiq.Net. The application indexes Turkish internal IT support procedures from Markdown files, retrieves relevant evidence for each question, and lets the agent answer with source citations in the embedded dashboard.

The sample uses the Runiq.Net architecture directly: `Runiq.AI.Core` hosts the dashboard, `Runiq.AI.Agents` hosts the assistant, and `Runiq.AI.Rag` owns ingestion, chunking, vector upsert, retrieval, grounding, observability, and the RAG runtime. Document content is evidence only; instructions inside retrieved documents must not override the user's request or the agent's system instructions.

## Run

An OpenAI API key is optional for startup ingestion because the sample uses a deterministic local embedding client. To get model-generated answers in the Playground, configure an OpenAI key without committing it:

```powershell
dotnet user-secrets init --project samples/Runiq.AI.Rag.ProductSupportAssistant
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_KEY" --project samples/Runiq.AI.Rag.ProductSupportAssistant
```

Run the sample:

```powershell
dotnet run --project samples/Runiq.AI.Rag.ProductSupportAssistant
```

Open `http://localhost:5198/dashboard`. On startup, the `product-support` RAG index ingests the copied bin-output corpus at `ProductSupportDocuments/corporate`. Then open **Corporate Document Assistant** in the Playground.

## RAG Configuration

The sample registers one RAG index:

```csharp
builder.Services.AddRuniqRag(rag => rag.AddIndex(ProductSupportAgent.IndexName, index => index
    .UseDirectory(Path.Combine(AppContext.BaseDirectory, "ProductSupportDocuments", "corporate"), searchPattern: "*.md")
    .UseEmbeddingModel("ollama/deterministic-sample")
    .UseInMemoryVectorStore()
    .ConfigureChunking(maxChunkLength: 900, chunkOverlap: 120)
    .ConfigureIngestion(ingestion => ingestion.OnStartup())));
builder.Services.AddRagEmbeddingClient(
    "ollama/deterministic-sample",
    _ => new DeterministicSampleEmbeddingClient());
```

The project file copies source documents from `SampleDocuments/corporate` into the application output as `ProductSupportDocuments/corporate`. Runtime ingestion reads from the bin output path, not from a temp folder or a mutable project-root runtime location.

## Knowledge Base

The sample corpus contains Markdown procedures for:

- VPN connection troubleshooting
- Password security
- System access requests
- External access approval
- Remote work technical rules
- Software requests
- IT support tickets
- Device assignment and return
- Data classification and file sharing
- Phishing and email security
- Security incident reporting
- Meeting room and equipment reservation

## Example Questions

```text
VPN baglantisi calismiyorsa ne yapmaliyim?
Yeni yazilim talebi nasil acilir?
Harici erisim izni icin hangi bilgiler gerekir?
Supheli MFA bildirimi alirsam ne yapmaliyim?
Gizli dosyalari kurum disina nasil paylasabilirim?
```

## Dashboard Inspection

Expand **RAG search** below an answer to inspect retrieved chunks, accepted and rejected candidates, source metadata, relevance values, and retrieval duration.
