# Runiq AI

**Build AI agents in C#. Run them inside your ASP.NET Core application.**

[![CI](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml?query=branch%3Amain)
[![.NET tests](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fruniq-net%2FRuniq.AI%2Fmain%2Fbadges%2Ftests.json)](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml?query=branch%3Amain)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![NuGet](https://img.shields.io/nuget/v/Runiq.AI.Agents?label=NuGet)](https://www.nuget.org/packages/Runiq.AI.Agents)
[![NuGet downloads for Runiq.AI.Agents](https://img.shields.io/nuget/dt/Runiq.AI.Agents?label=downloads)](https://www.nuget.org/packages/Runiq.AI.Agents)
[![Documentation](https://img.shields.io/badge/docs-runiq.net-blue)](https://runiq.net/docs)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Runiq AI is a code-first agent runtime for .NET. Define agents and strongly typed tools, stream model responses, ground answers in your documents, and orchestrate workflows using your application's hosting and dependency injection. An embedded dashboard lets you explore agents, try conversations, and inspect runtime activity.

[Quickstart](#quickstart) · [Packages](#packages) · [Samples](#samples) · [Build and test](#build-and-test) · [Documentation](https://runiq.net/docs)

## What you can build

- **Agents with tools:** C# agent definitions, typed tool inputs and outputs, provider integration, and streaming execution.
- **Document assistants:** ingestion, chunking, embeddings, retrieval, grounding policies, source citations, and optional reranking.
- **Persistent knowledge stores:** PostgreSQL and pgvector integration for durable documents and vector search.
- **Agent workflows:** code-first orchestration with runtime and dashboard integration.
- **MCP services:** expose application capabilities to MCP-compatible clients over HTTP.
- **Application-owned dashboards:** host the dashboard in your ASP.NET Core process with configurable authentication.

## Quickstart

You need the .NET 10 SDK and an OpenAI API key for this example.

### 1. Create an application

```powershell
dotnet new web -n MyRuniqApp
cd MyRuniqApp
dotnet add package Runiq.AI.Agents --version 1.0.0
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_OPENAI_API_KEY"
```

`Runiq.AI.Agents` includes the agent registration API and references `Runiq.AI.Core` and `Runiq.AI.Rag`. You do not need to install those dependencies separately for this example.

### 2. Replace `Program.cs`

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Core;

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("Configure OpenAI:ApiKey before starting the app.");

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
        id: "assistant",
        name: "Assistant",
        instructions: "You are a helpful assistant. Give clear, concise answers.",
        model: "openai/gpt-5",
        apiKey: apiKey));
});

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "My Runiq App";
    options.Authentication(auth =>
    {
        // Local demo only. Configure authentication before deploying.
        auth.AllowAnonymous();
    });
});

app.Run();
```

### 3. Run and explore

```powershell
dotnet run --environment Development --urls http://localhost:5050
```

Open [localhost:5050/dashboard](http://localhost:5050/dashboard), select **Assistant**, and start a conversation. Model requests use your configured provider credentials.

For authenticated hosting, see the [user-based](samples/Runiq.AI.DashboardSecurityUser/README.md) and [role-based](samples/Runiq.AI.DashboardSecurityRole/README.md) security samples.

Prefer scaffolding? Install the [Runiq CLI](src/Runiq.AI.Cli/README.md) and run its project wizard:

```powershell
dotnet tool install --global Runiq.AI.Cli --version 1.0.0
runiq init MyRuniqApp
```

## Packages

Choose packages by capability. Package guides include configuration and API examples.

| Package | Purpose | Guide |
| --- | --- | --- |
| `Runiq.AI.Agents` | Agent definitions, typed tools, providers, streaming, and execution results | [Agents](src/Runiq.AI.Agents/README.md) |
| `Runiq.AI.Core` | Shared contracts, ASP.NET Core hosting, runtime endpoints, and embedded dashboard | [Core](src/Runiq.AI.Core/README.md) |
| `Runiq.AI.Rag` | Document ingestion, chunking, embeddings, vector storage, and retrieval | [RAG](src/Runiq.AI.Rag/README.md) |
| `Runiq.AI.Rag.PostgreSql` | PostgreSQL persistence and pgvector search | [PostgreSQL](src/Runiq.AI.Rag.PostgreSql/README.md) |
| `Runiq.AI.Workflows` | Code-first workflow definitions and execution | [Workflows](src/Runiq.AI.Workflows/README.md) |
| `Runiq.AI.Mcp` | MCP server integration and application tools | [MCP](src/Runiq.AI.Mcp/README.md) |
| `Runiq.AI.Cli` | .NET tool for scaffolding applications | [CLI](src/Runiq.AI.Cli/README.md) |

## Ground answers in your documents

RAG agents support three execution modes: `Open`, `Grounded`, and `Required`. Configure what happens when no acceptable context is found, apply relevance thresholds, and optionally rerank accepted candidates before selecting context.

- [Grounding policies and relevance acceptance](src/Runiq.AI.Agents/README.md#rag-execution-and-grounding-policies)
- [Cohere reranking configuration](src/Runiq.AI.Agents/README.md#cohere-production-reranker)
- [Answerability and failure behavior](src/Runiq.AI.Agents/README.md#answerability-acceptance-criteria)
- [Provider-neutral reranking contract](src/Runiq.AI.Rag/README.md#optional-reranking-contract)

Agent Chat distinguishes selected grounding evidence from validated source citations. Citation validation checks source identity; it does not establish that a source supports every sentence. Remote reranking sends the query and candidate chunk text to the configured provider; review the [operational and security criteria](src/Runiq.AI.Agents/README.md#performance-and-operational-acceptance-criteria) before enabling it.

## Samples

| Sample | Demonstrates |
| --- | --- |
| [Expense assistant](samples/Runiq.AI.Expense/README.md) | Application agents and tools in an expense scenario |
| [Product support assistant](samples/Runiq.AI.Rag.ProductSupportAssistant/README.md) | Document ingestion and RAG-powered support |
| [Travel planner](samples/Runiq.AI.WorkflowTravelPlanner/README.md) | Agent workflow orchestration |
| [User-based dashboard security](samples/Runiq.AI.DashboardSecurityUser/README.md) | Dashboard access with user authentication |
| [Role-based dashboard security](samples/Runiq.AI.DashboardSecurityRole/README.md) | Dashboard access with role-based authorization |

Each sample guide describes its configuration and run commands.

## Build and test

Run these commands from the repository root. PostgreSQL integration tests require Docker with Compose and use the local pgvector service on port `54329`.

```powershell
docker compose -f docker-compose.rag-postgresql.yml up -d --wait
dotnet restore Runiq.AI.slnx
dotnet build Runiq.AI.slnx --no-restore -c Release
dotnet test Runiq.AI.slnx --no-build -c Release --logger trx --results-directory TestResults
```

When finished with the database:

```powershell
docker compose -f docker-compose.rag-postgresql.yml stop
```

The dashboard frontend has separate build and test commands in its [development guide](src/Runiq.Dashboard.Client/README.md).

### CI and test reporting

The **CI** badge reports the `main` branch workflow status. The **.NET tests** badge reports executed, passed, failed, and skipped counts from the latest published `main` run's TRX reports. Skipped tests are excluded from the executed count. Counts are generated by CI, never maintained manually, and exclude the dashboard JavaScript suite.

Click either badge to open GitHub Actions. Each run includes a **.NET test results** summary and a **test-results** artifact containing the available TRX reports and badge JSON, including when tests fail. Pull requests produce their own summaries and artifacts; only `main` push runs update the README badge. The initial badge reads **awaiting CI results** until CI publishes its first report.

Badge publication requires the workflow token to have repository write access and branch rules to allow the existing bot update. If publication is blocked, the run summary and artifact still contain the results; consult the workflow status for the current run. Shields may briefly cache an older badge.

To verify the reporting script locally with synthetic TRX cases:

```powershell
pwsh -File scripts/test-update-test-badge.ps1
```

### Create local NuGet packages

After a successful Release build and test run:

```powershell
dotnet pack Runiq.AI.slnx --no-build -c Release -o artifacts/packages/1.0.0
```

Package versions are defined in the project files and currently use `1.0.0`. This command creates local packages only. Packaging may report `NU5104` because `Runiq.AI.Rag` references the prerelease dependency `UglyToad.PdfPig` version `1.7.0-custom-5`.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Runiq.AI.*` | .NET runtime packages and CLI |
| `src/Runiq.Dashboard.Client` | Dashboard frontend |
| `samples/` | Example applications |
| `tests/` | .NET test projects |
| `scripts/` | Repository automation and test badge reporting |
| `.github/workflows/` | Build, test, and package CI |

## Contributing and support

Read the [contribution guide](CONTRIBUTING.md) for local setup and contribution conventions. Use [GitHub Issues](https://github.com/runiq-net/Runiq.AI/issues) for reproducible bugs and feature requests, and [runiq.net/docs](https://runiq.net/docs) for documentation.

## License

Runiq AI is licensed under the [MIT License](LICENSE).
