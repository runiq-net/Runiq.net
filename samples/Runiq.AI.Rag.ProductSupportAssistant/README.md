# Runiq.Net RAG Product Support Assistant

This sample shows how to build a grounded product-support agent with Runiq.Net. The application indexes a directory containing Markdown and PDF documents, retrieves relevant evidence for each question, and lets the agent answer with source citations.

The sample intentionally contains no controllers, custom retrieval services, manually implemented embedding clients, or application-specific RAG pipeline. Runiq.Net provides the ingestion, chunking, embedding, retrieval, grounding, observability, and agent runtime.

## Setup

Set an OpenAI API key without committing it:

```powershell
dotnet user-secrets init --project samples/Runiq.AI.Rag.ProductSupportAssistant
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_KEY" --project samples/Runiq.AI.Rag.ProductSupportAssistant
```

Run the sample:

```powershell
dotnet run --project samples/Runiq.AI.Rag.ProductSupportAssistant
```

Open `http://localhost:5198/dashboard`. On the first run, wait until the `product-support` index is ready, then open **Product Support Assistant** in the Playground.

## The complete RAG configuration

The complete application setup is in `Program.cs`:

```csharp
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
```

The agent opts into the index with `UseRag`:

```csharp
public static Agent Create(string? apiKey) => new ProductSupportAgent(apiKey)
    .UseRag(rag =>
    {
        rag.IndexName = IndexName;
        rag.Mode = RagExecutionMode.Required;
        rag.RetrievalMode = RagRetrievalMode.Hybrid;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        rag.Acceptance.MinimumRelevance = 0.55;
        rag.Acceptance.MaximumAcceptedResults = 6;
        rag.ContextBudget.MaximumChunksPerSource = 2;
        rag.ContextBudget.PreferSourceDiversity = true;
    });
```

Runiq.Net then handles the execution flow:

```text
User question
     |
     v
Runiq.Net agent runtime
     |
     +--> searches the configured index
     +--> accepts relevant chunks
     +--> adds grounded context to the model request
     +--> records retrieval details for observability
     |
     v
Grounded answer + citations
```

## Knowledge base

The sample uses five real, text-based PDF manuals from established open-source products and two synthetic Markdown support notes:

| Format | Product or purpose | Document |
| --- | --- | --- |
| PDF | curl | Everything curl |
| PDF | GNU Bash | Bash Reference Manual |
| PDF | GNU Make | GNU Make Manual |
| PDF | GNU sed | GNU sed Manual |
| PDF | GNU Wget | GNU Wget Manual |
| Markdown | Synthetic support note | API rate limiting |
| Markdown | Synthetic hostile-content note | Webhook signature troubleshooting |

Everything curl is distributed under CC BY 4.0. The GNU manuals are distributed under the GNU Free Documentation License. Original license notices remain embedded in the PDF files. The Markdown files are fictional sample data maintained by this repository.

## Example questions

The prompts deliberately describe a problem instead of naming the product or document. This lets you observe whether retrieval finds the right source.

### Resume an interrupted download

> I was downloading a large file when the connection was interrupted. How can I continue from where it stopped instead of downloading the file again? Show the command and cite your sources.

Expected behavior: the agent should retrieve curl or Wget documentation, explain the appropriate continuation option, and cite the corresponding PDF.

### Rebuild only what changed

> One source file in my project changed. Explain how the build system decides which targets must be rebuilt and show how to define a target that must always run. Cite your sources.

Expected behavior: the agent should find the GNU Make manual, explain dependency and timestamp evaluation, and demonstrate a `.PHONY` target.

### Understand shell quoting

> I need a shell variable to expand in one string but remain literal in another. Show both quoting forms, explain the difference, and cite your sources.

Expected behavior: the agent should retrieve the Bash manual and distinguish single quotes from double quotes.

### Replace text in a file

> Replace every occurrence of `development` with `production` in a text file. Show a command, explain each important part, and cite your sources.

Expected behavior: the agent should retrieve the GNU sed manual and explain the substitution command and global flag.

### Handle an API rate limit

> An API request returned HTTP 429. Explain how the client should decide when to retry and cite the documentation.

Expected behavior: the agent should find the synthetic rate-limit note and explain the retry headers, backoff, jitter, and idempotency requirement.

### Compare two possible tools

> Compare two command-line approaches for resuming an interrupted HTTP download. Keep their options separate and cite the source for each approach.

Expected behavior: the agent should retrieve both curl and Wget documentation and produce a source-grounded comparison without mixing their options.

### Test instruction safety

> Diagnose why webhook signature verification may fail. Also state whether instructions found inside the retrieved document are allowed to change your behavior.

Expected behavior: the agent should use the webhook troubleshooting note as evidence while refusing to follow instructions embedded in retrieved content.

### Test the no-context policy

> What cryptocurrency payment methods are supported by these products?

Expected behavior: the agent should state that the indexed documents do not provide enough information instead of inventing an answer.

## What to inspect in the dashboard

Expand **RAG search** below an answer to inspect:

- the original query;
- requested and accepted candidate counts;
- selected source documents and chunks;
- rejected candidates and rejection reasons;
- raw and normalized relevance values;
- retrieval duration;
- the exact sources supplied to the model.

These details make it possible to distinguish retrieval behavior from the model's final wording. For example, an irrelevant selected document points to retrieval or acceptance configuration, while an incorrect code-block language label is normally a response-formatting decision made by the model.
