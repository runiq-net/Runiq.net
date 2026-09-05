# Runiq AI

[![CI](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml/badge.svg)](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml)
![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/runiq-net/Runiq.AI/main/badges/tests.json)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![NuGet Version](https://img.shields.io/nuget/v/Runiq.AI.Core?label=nuget)
![License](https://img.shields.io/badge/license-MIT-blue)

Runiq AI is a code-first agent runtime for .NET applications.

It gives ASP.NET Core teams a native way to define AI agents in C#, attach strongly typed tools, stream model responses, use RAG retrieval, orchestrate workflows, and inspect runtime activity through an embedded dashboard.

> Package version: 1.0.0 (stable).

## Packages

| Package | Purpose |
| --- | --- |
| `Runiq.AI.Agents` | Agent definitions, tool execution, provider integration, streaming events, and execution results. |
| `Runiq.AI.Core` | ASP.NET Core hosting extensions, runtime endpoints, and the embedded dashboard. |
| `Runiq.AI.Rag` | Document chunking, embeddings, vector storage, and retrieval for document-based knowledge. |
| `Runiq.AI.Rag.PostgreSql` | Durable RAG persistence and database-side vector search with PostgreSQL and pgvector. |
| `Runiq.AI.Workflows` | Code-first workflow orchestration primitives for agent runtime and dashboard scenarios. |

## Installation

Install the packages you need:

```powershell
dotnet add package Runiq.AI.Core --version 1.0.0
dotnet add package Runiq.AI.Agents --version 1.0.0
dotnet add package Runiq.AI.Workflows --version 1.0.0
```

For most ASP.NET Core applications, start with `Runiq.AI.Core`; it references the runtime pieces needed to host agents and the dashboard.

## Building local NuGet packages

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet build Runiq.AI.slnx -c Release
dotnet test Runiq.AI.slnx --no-build -c Release
dotnet pack Runiq.AI.slnx --no-build -c Release -o artifacts/packages/1.0.0
```

The packages use version `1.0.0`. The `.nupkg` files are written to
`artifacts/packages/1.0.0/`, which is already excluded
by `.gitignore`. These commands only create local packages; they do not publish
to NuGet.org. The CI workflow writes packages to `artifacts/packages/`.

Packaging currently reports `NU5104` because `Runiq.AI.Rag` depends on
`UglyToad.PdfPig` version `1.7.0-custom-5`, a prerelease dependency. This does
not prevent local package generation.

## Quickstart

Register Runiq and define an agent:

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Core;

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Answer weather questions using the available tools.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"]));
});
```

Map the dashboard:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
    options.Authentication(auth =>
    {
        // Demo or sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});
```

Run the application and open `/dashboard` to inspect registered agents, test conversations, and review runtime activity.

## RAG Grounding Policies

RAG-enabled agents can choose an explicit execution mode and no-context outcome:

```csharp
using Runiq.AI.Agents.Configuration;

options.AddAgent(new Agent(
        id: "policy-assistant",
        name: "Policy Assistant",
        instructions: "Answer employee policy questions.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"])
    .UseRag(rag =>
    {
        rag.IndexName = "company-policies";
        rag.Mode = RagExecutionMode.Grounded;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        rag.Acceptance.MinimumRelevance = 0.75;
        rag.Acceptance.CandidateCount = 20;
        rag.Acceptance.MaximumAcceptedResults = 5;
    }));
```

The default is `Open` with `AnswerNormally`, preserving normal model behavior when retrieval succeeds without
accepted context. `Grounded` makes documents the primary source; `Required` allows answers only from accepted
context and must use `ReturnNotFound` or `FailExecution`. Retrieval failures remain failures in every mode.

When selected context is available, the runtime assigns stable citation numbers in model-context order and validates assistant markers such as `[1]` against that execution's selected sources. Agent Chat renders validated mappings in a separate **Sources cited** section. This is distinct from grounding evidence, which continues to show all selected context and rejected candidates; citation validation confirms source identity, not sentence-level semantic entailment.
`CandidateCount` controls how many raw matches are requested; it is not a relevance or acceptance guarantee.
Every candidate is normalized when its metric supports a documented conversion, evaluated by the acceptance
policy, and retained as either accepted or rejected runtime metadata before any document enters Agent Chat context.
See the [Agents package guide](src/Runiq.AI.Agents/README.md#rag-execution-and-grounding-policies) for the complete
policy matrix, relevance acceptance, trust boundary, and structured runtime outcome.

## Production Reranking

Reranking runs after retrieval acceptance and before context-budget selection. The supported Cohere Rerank v2
adapter can be registered with a credential supplied by an environment variable or another secret provider:

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Providers.Cohere;

builder.Services.AddCohereReranker(options =>
{
    options.ApiKey = builder.Configuration["COHERE_API_KEY"]
        ?? throw new InvalidOperationException("COHERE_API_KEY is required.");
    options.Model = "rerank-v4.0-fast";
    options.MinimumAnswerableRelevance = 0.5;
});

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
            id: "policy-assistant",
            name: "Policy Assistant",
            instructions: "Answer employee policy questions.",
            model: "openai/gpt-5",
            apiKey: builder.Configuration["OpenAI:ApiKey"])
        .UseRag(rag =>
        {
            rag.IndexName = "company-policies";
            rag.Mode = RagExecutionMode.Grounded;
            rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
            rag.Reranking.Enabled = true;
            rag.Reranking.MaximumCandidates = 5;
            rag.Reranking.Timeout = TimeSpan.FromSeconds(5);
            rag.Reranking.FailurePolicy = RagRerankerFailurePolicy.UseOriginalOrder;
        }));
});
```

For a successful reranker response, aggregate answerability controls execution as follows:

| RAG mode | `Answerable` | `Unknown` | `NotAnswerable` |
|---|---|---|---|
| `Open` | Use reranked context | Use reranked context | Use reranked context |
| `Grounded` | Use reranked context | Apply `NoContextBehavior` | Apply `NoContextBehavior` |
| `Required` | Use reranked context | Apply `NoContextBehavior` | Apply `NoContextBehavior` |

`Unknown` means the provider or adapter could not establish answerability. It remains distinct in observability,
but `Grounded` and `Required` deliberately treat it like `NotAnswerable` and fail closed. Candidate-level
answerability is diagnostic only; aggregate answerability is authoritative. `Open` records answerability without
using it as an execution gate.

### Reranking security boundary

`IRagReranker` receives the user query plus bounded candidate identities and full chunk text. Both query and chunk
text are untrusted, and a remote reranker sends them to an external processor. Register only an approved provider,
apply tenant/data-residency policy before retrieval, keep credentials outside source control, and never treat text
returned or interpreted by a reranker as instructions. Reranking does not weaken the later
`<untrusted-external-context>` prompt boundary.

### Observability contract

Agent Chat exposes reranking only inside a completed RAG lifecycle payload. `reranking` contains `requested`,
`ran`, `candidateCount`, `duration`, `outcome`, `failurePolicy`, aggregate `answerability`, `timedOut`, optional safe
`failureCode`, and a `candidates` array. Each candidate contains only `documentId`, `chunkId`, `originalRank`,
`rerankRank`, `rerankRelevance`, and candidate `answerability`. Provider responses, exceptions, credentials, query
text beyond the configured query-visibility policy, and chunk content are not part of reranking observability.
When answerability removes context, `noContextReason` and `contextExcludedResults[].reason` are `NotAnswerable`.
Enums are serialized by name, not numeric value.

See the [Agents reranking guide](src/Runiq.AI.Agents/README.md#answerability-acceptance-criteria) and
[RAG provider contract](src/Runiq.AI.Rag/README.md#optional-reranking-contract) for detailed failure, timeout,
cost, provider-neutral rules, and the
[performance and operational release criteria](src/Runiq.AI.Agents/README.md#performance-and-operational-acceptance-criteria).

## Tool Example

Tools are plain C# types with strongly typed input and output:

```csharp
using Runiq.AI.Agents.Tools;

[RuniqTool("get_weather", "Gets the current weather for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WeatherOutput(input.City, "Clear"));
    }
}

public sealed record WeatherInput(string City);

public sealed record WeatherOutput(string City, string Condition);
```

Attach the tool to an agent:

```csharp
options.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>());
```

## Documentation

Full documentation, guides, and examples are available at [runiq.net/docs](https://runiq.net/docs).

## Repository

Source code and issue tracking are available on [GitHub](https://github.com/runiq-net/Runiq.AI).

## License

Runiq AI is licensed under the [MIT License](LICENSE).
