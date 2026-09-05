# Runiq.AI.Rag

## Optional reranking contract

`IRagReranker` is the provider-neutral second-stage reranking boundary. It receives the current query and a bounded
ordered set of accepted chunk identities, content, and original ranks. A provider returns one `[0,1]`,
higher-is-better relevance score and candidate answerability signal for every requested identity, plus aggregate
`RagAnswerability`. Agent runtime configuration owns candidate limits, timeout, failure/fallback behavior, and
grounding enforcement. Reranking never overwrites retrieval scores or provenance.

The reranker boundary receives the original user query and the full text of each bounded accepted chunk. Treat
both as untrusted application data. A remote implementation creates an explicit data-egress boundary, so hosts
must select an approved provider, enforce tenant and data-residency requirements before retrieval, and keep API
credentials in a secret provider. Candidate content is input for scoring only: it must never be promoted to
system/developer instructions, logged by the adapter, copied into errors, or added to observability payloads.

## Retrieval modes

`RagQuery.Mode` selects `Semantic`, `Lexical`, or `Hybrid`; omitting it preserves the existing semantic
default. Semantic mode generates a query embedding and performs only vector retrieval. Lexical mode performs
only indexed lexical retrieval and therefore works without an `IEmbeddingClient`. Hybrid mode requires both
sources and fails if either source fails.

```csharp
var identifiers = await retriever.RetrieveAsync(new RagQuery
{
    IndexName = "engineering",
    Text = "CS1503",
    Mode = RagRetrievalMode.Lexical,
});

var phrase = await retriever.RetrieveAsync(new RagQuery
{
    IndexName = "engineering",
    Text = "\"dependency injection\"",
    Mode = RagRetrievalMode.Hybrid,
});
```

Surrounding double quotes express exact phrase intent. PostgreSQL uses the `simple` text-search configuration
plus a trigram index so punctuation-sensitive identifiers such as `POL-HR-014`, `IRagRetriever`, and
`filename.cs` remain searchable. Hybrid results are merged by `(documentId, chunkId)` and ranked with
reciprocal rank fusion, `1 / (60 + sourceRank)`, using one-based ranks. Provider semantic and lexical scores
are retained in `RagRetrievalProvenance` but are never added, averaged, normalized, or compared to each other.

Runiq.AI.Rag provides retrieval-augmented generation primitives for Runiq AI applications.

It includes abstractions and default implementations for document chunking, embedding generation, vector storage, retrieval, and search result mapping.

The built-in in-memory store is intended for development and tests. Durable logical index, document, chunk,
embedding, metadata, and ingestion-state persistence with database-side vector search is available from the separate
`Runiq.AI.Rag.PostgreSql` package; the core package has no Npgsql or pgvector dependency.

## Retrieval Score Semantics

Retrieval results distinguish the provider's `RawScore` from nullable provider-independent `Relevance`.
`Metric` and `HigherIsBetter` define how the raw value must be interpreted; raw scores are never presented as
provider-independent confidence. Normalized relevance, when available, is always in the inclusive `[0,1]` range.

The in-memory adapter exposes cosine similarity as higher-is-better and normalizes it with `(raw + 1) / 2`. It
exposes Euclidean distance as lower-is-better and normalizes it with `1 / (1 + raw)`. Dot product is unbounded, so
the adapter reports its raw higher-is-better score with `Relevance = null` instead of inventing a normalization.
Provider adapters with a documented, reliable transformation may supply normalized relevance for their own metric.

`TopK` and `RagQuery.TopK` are candidate limits only. Agent context acceptance, duplicate filtering, relevance
thresholds, and maximum accepted-result limits are applied later by the single Agent runtime policy path described
in the [Agents package guide](../Runiq.AI.Agents/README.md#rag-execution-and-grounding-policies).

## Installation

```powershell
dotnet add package Runiq.AI.Rag --version 1.0.0
```

## Named index ingestion strategies

Named indexes default to `Manual`, so registering a large corpus never unexpectedly blocks application startup.
Select an explicit lifecycle contract with `ConfigureIngestion`: `Manual`, blocking `OnStartup`, non-blocking
`BackgroundOnStartup`, or `Scheduled`. Registration stores immutable configuration only; it does not scan files,
start ingestion, create background work, or run a scheduler.

Use typed provider conveniences for built-in selections and retain string references only for custom registrations:

```csharp
services.AddRuniqRag(rag => rag.AddIndex("corporate-documents", index => index
    .UseDirectory("./documents", "*.md", recursive: true)
    .UseOpenAiEmbeddingModel(OpenAiEmbeddingModels.TextEmbedding3Small)
    .UseInMemoryVectorStore()
    .ConfigureIngestion(ingestion => ingestion.OnStartup())));
```

RAG observability is content-free by default. Retrieved document and chunk content remains available to model context assembly but is not copied into client payloads. Applications may opt into small local-development previews explicitly:

```csharp
services.AddRuniqRag(rag =>
{
    rag.ConfigureObservability(options =>
    {
        options.QueryVisibility = RagQueryVisibility.Redacted;
        options.ContentPreview.Enabled = true;
        options.ContentPreview.MaximumCharacters = 160;
        options.ContentPreview.IncludeSelectedResults = true;
    });
});
```

Preview values pass through `IRagObservabilityRedactor` before normalization and truncation. Enabling previews does not expose API keys, provider diagnostics, or source paths, and debug mode does not bypass these boundaries.

`OpenAiEmbeddingModels` and `UseOpenAiEmbeddingModel` are provided by `Runiq.AI.Agents.Providers.OpenAI`, keeping
provider-specific identities outside the provider-neutral RAG package. Schedule expressions use five fields and the
runtime's default time-zone policy.

## Managed ingestion runtime

Resolve `IRagIngestionManager` to start, inspect, and cancel an index operation. Runtime state and the most recent
operation are held in memory and reset when the process restarts; registry metadata remains static configuration.
`OnStartup` blocks host startup, `BackgroundOnStartup` runs through managed background execution, and `Scheduled`
uses a lightweight local in-process five-field scheduler. Scheduled execution is intentionally per process: it does
not provide distributed locking, leader election, or multi-instance coordination.

## Dashboard management API

When RAG and the embedded Dashboard are registered together, the Dashboard exposes management endpoints under its
configured API base path: `GET /api/rag/indexes`, `GET /api/rag/indexes/{indexName}`,
`GET /api/rag/indexes/{indexName}/status`, `POST /api/rag/indexes/{indexName}/ingestion/start`, and
`POST /api/rag/indexes/{indexName}/ingestion/cancel`. These endpoints use the Dashboard's configured access policy.

The API projects explicit hosting DTOs and safe registry display metadata; it does not serialize domain models,
provider credentials, raw paths, document content, or exceptions. Readiness and operation state remain separate.
All registered indexes support an explicit manual run, including indexes whose strategy is `OnStartup`,
`BackgroundOnStartup`, or `Scheduled`; the strategy controls automatic triggers only. Start and cancel commands are
coordinated exclusively by `IRagIngestionManager`, including conflict and cancellation behavior.

## Related Packages

| Package | Purpose |
| --- | --- |
| `Runiq.AI.Agents` | Agent definitions, tool execution, provider integration, streaming events, and execution results. |
| `Runiq.AI.Core` | ASP.NET Core hosting extensions, runtime endpoints, and the embedded dashboard. |
| `Runiq.AI.Workflows` | Code-first workflow orchestration primitives for agent runtime and dashboard scenarios. |
