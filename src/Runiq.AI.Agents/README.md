# Runiq.AI.Agents

![NuGet Version](https://img.shields.io/nuget/vpre/Runiq.AI.Agents?label=nuget)

Code-first AI agents for .NET.

`Runiq.AI.Agents` provides the core agent model for Runiq AI. Use it to define agents in C#, attach strongly typed tools, configure model providers, and build agent-based applications with structured execution support.

## Why Runiq.AI.Agents?

Runiq.AI.Agents is designed for .NET developers who want to build AI agents without leaving the C# ecosystem.

It focuses on:

- Code-first agent definitions
- Strongly typed tool execution
- Provider-aware model configuration
- Runtime-friendly agent composition
- Streaming and structured execution support
- Integration with the broader Runiq AI platform

## Install

```powershell
dotnet add package Runiq.AI.Agents --prerelease
```

## Create an Agent

```csharp
using Runiq.AI.Agents;

var agent = new Agent(
    id: "weather-agent",
    name: "Weather Agent",
    instructions: "Answer weather questions using the available tools.",
    model: "openai/gpt-5",
    apiKey: configuration["OpenAI:ApiKey"]);
```

An agent contains the basic runtime definition:

- `id`: stable identifier used by the runtime
- `name`: human-readable agent name
- `instructions`: system-level behavior definition
- `model`: target model identifier
- `apiKey`: provider credential

## Add a Tool

Tools allow agents to call strongly typed C# code.

```csharp
using Runiq.AI.Agents.Tools;

[RuniqTool("get_weather", "Gets the current weather for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new WeatherOutput(input.City, "Clear"));
    }
}

public sealed record WeatherInput(string City);

public sealed record WeatherOutput(string City, string Condition);
```

Attach the tool to the agent:

```csharp
var agent = new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>();
```

## RAG Execution and Grounding Policies

Configure framework-owned retrieval and grounding through `UseRag`:

```csharp
using Runiq.AI.Agents.Configuration;

var agent = new Agent(
        id: "policy-assistant",
        name: "Policy Assistant",
        instructions: "Answer employee policy questions.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .UseRag(rag =>
    {
        rag.IndexName = "company-policies";
        rag.Mode = RagExecutionMode.Required;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        rag.Acceptance.MinimumRelevance = 0.75;
        rag.Acceptance.CandidateCount = 20;
        rag.Acceptance.MaximumAcceptedResults = 5;
        rag.Reranking.Enabled = true;
        rag.Reranking.MaximumCandidates = 5;
        rag.Reranking.Timeout = TimeSpan.FromSeconds(5);
        rag.Reranking.FailurePolicy = RagRerankerFailurePolicy.UseOriginalOrder;
        rag.ContextBudget.MaximumContextTokens = 32_768;
        rag.ContextBudget.ResponseTokenReserve = 4_096;
        rag.ContextBudget.MaximumChunksPerSource = 2;
        rag.ContextBudget.PreferSourceDiversity = true;
    });
```

`Open` is the default execution mode. It uses accepted document context when available and, with the default
`AnswerNormally` behavior, preserves normal agent execution when retrieval returns no accepted context.
`Grounded` treats accepted documents as the primary source, requires unsupported information to be separated,
forbids invented company policies, and requires conflicting sources to be identified. `Required` constrains the
answer to accepted context and therefore cannot be combined with `AnswerNormally`; that combination fails during
configuration and is validated again before retrieval at runtime.

No-context behavior is selected independently for every valid combination:

| Behavior | Outcome when no context is accepted |
|---|---|
| `AnswerNormally` | Invokes the model without accepted context. Invalid with `Required`. |
| `ReturnNotFound` | Returns a successful framework-owned not-found response and skips the model. |
| `FailExecution` | Returns `RagContextUnavailable` as an execution failure and skips the model. |

`CandidateCount` (default 20) is the vector-search candidate budget, not an accepted-result guarantee.
`MaximumAcceptedResults` (default 5) limits context after every candidate has been evaluated; otherwise acceptable
results outside the limit remain visible as `ResultLimitExceeded` rejections. `MinimumRelevance` is an optional
threshold in the inclusive provider-independent `[0,1]` range. The default null threshold does not manufacture
relevance for an unsupported metric.

Reranking is disabled by default. When enabled, the runtime sends at most `MaximumCandidates` accepted results to
the registered provider-neutral `IRagReranker` after acceptance and before token-budget selection. A rerank score
is always a separate `[0,1]` higher-is-better value; it never replaces semantic, lexical, RRF, raw, or normalized
retrieval scores. Equal scores use original accepted rank and then ordinal document/chunk identity. The reranker
must return every requested identity exactly once; unknown, duplicate, missing, non-finite, or out-of-range output
invalidates the complete response.

`FailurePolicy = Fail` prevents model execution. `UseOriginalOrder` preserves the exact accepted retrieval order
for unavailable services, timeouts, exceptions, and invalid output, and exposes the fallback through structured
metadata. Caller cancellation still propagates, while `Timeout` is classified separately. Aggregate and
per-candidate `RagAnswerability` are observable. `Grounded` and `Required` apply their configured no-context policy
when a successful reranker result is not `Answerable`; `Open` keeps the evidence and continues normally.

Context selection is a separate stage after acceptance. The runtime calculates
`MaximumContextTokens - instructions - conversation history - user query - response reserve - other required prompt`
and selects only complete chunks whose final serialized external-context message fits. The deterministic fallback
estimator counts contiguous Unicode letter/digit runs and individual punctuation marks; it does not call a model
and is explicitly an estimate rather than an exact provider token count. The defaults are 32,768 maximum context
tokens and a 4,096-token response reserve. `MaximumChunksPerSource` defaults to `int.MaxValue` for compatibility;
set a bounded value to prevent one document from monopolizing context. `PreferSourceDiversity` performs stable
source rounds while retaining retrieval order within each source. Chunks are never silently truncated.

Accepted results omitted from model context remain available through `ContextExcludedResults` with
`TokenBudgetExceeded`, `OverlappingContent`, or `SourceLimitExceeded`. Overlap reduction uses character boundaries
from stable same-document chunk metadata and keeps the earlier retrieval result when at least half of the shorter
span overlaps. `ContextBudget` exposes count-only estimates and totals without exposing prompt or document text.
If mandatory prompt content plus the response reserve exceeds the maximum, execution fails with
`RagContextBudgetExceeded` before model invocation. When accepted results exist but none fit, the no-context reason
is `ContextBudgetExhausted`, and the configured grounding/no-context policy remains authoritative.

The framework keeps raw provider score semantics separate from normalized relevance. Cosine similarity in
`[-1,1]` is normalized with `(raw + 1) / 2`; non-negative Euclidean distance is normalized with `1 / (1 + raw)`.
Cosine is higher-is-better and Euclidean distance is lower-is-better. Unbounded dot product has no universal
normalization, so `Relevance` remains null. A provider-specific policy can explicitly accept such candidates:

```csharp
using Runiq.AI.Rag.Models.Search;

rag.Acceptance.ProviderSpecificAcceptance = result =>
    result.Metric == RagScoreMetrics.DotProduct && result.RawScore >= 2.5;
```

Missing metrics, inconsistent metric direction, NaN, infinity, and relevance outside `[0,1]` are retained as
`InvalidScore` rejections. Duplicate content, threshold failures, and accepted-result overflow are retained as
`DuplicateContent`, `BelowMinimumRelevance`, and `ResultLimitExceeded`. Equal relevance uses ordinal document ID
and chunk ID ordering, so identical query/index/provider configuration produces stable context order.

A successful empty retrieval reports `NoResults`; a successful retrieval whose candidates are all rejected reports
`BelowRelevanceThreshold` when every rejection is threshold-based, otherwise `CandidatesRejected`. Retrieval
exceptions remain `RagRetrievalFailed` and are never converted into a no-context result or normal answer.

The runtime emits framework grounding rules as authoritative instructions and sends accepted document text only
inside a separate `<untrusted-external-context>` user message. Document instructions are treated as untrusted
data and cannot be promoted into system, developer, agent, or framework instruction authority. This boundary is
a prompt-injection mitigation, not a guarantee that a model can never be manipulated.

`AgentExecutionResult.Rag`, terminal `AgentExecutionEvent.Rag`, and Agent Chat result responses expose
the applied mode, accepted-context status, applied no-context behavior and reason, whether model invocation was
skipped, and whether the framework constrained the answer to accepted context. The metadata also exposes ordered
candidate, accepted, and rejected collections and their counts; every item carries raw score, normalized relevance,
metric, direction, and any rejection reason. Streaming and non-streaming executions share this same evaluation.
`IsAnswerGrounded` reports the applied framework policy; it is not independent semantic verification of model output.
Agent Chat SSE projects the content-free RAG search lifecycle through dedicated `rag_search_started`,
`rag_search_completed`, and `rag_search_failed` events instead of serializing runtime result collections.

## Tool Design

A Runiq tool is a regular C# class that implements:

```csharp
IRuniqTool<TInput, TOutput>
```

This gives you:

- Strongly typed input models
- Strongly typed output models
- Testable business logic
- Clean separation between agent behavior and application code

## Typical Use Cases

Use `Runiq.AI.Agents` when you want to build:

- AI assistants for .NET applications
- Tool-using agents
- Domain-specific agents
- Agent workflows
- Internal automation agents
- Dashboard-observable agent runtimes
- MCP-compatible agent experiences

## Related Packages

Runiq AI is modular. `Runiq.AI.Agents` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Core` | Hosts agents and the embedded dashboard in ASP.NET Core |
| `Runiq.AI.Rag` | Owns document-based retrieval, vector indexes, and RAG query primitives |
| `Runiq.AI.Workflows` | Orchestrates agents in code-first workflows |
| `Runiq.AI.Mcp` | Exposes ASP.NET Core applications through MCP-compatible tools |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

Runiq AI is currently in preview.

APIs may change before the first stable release.

The main direction is clear:

> Build code-first AI agents, tools, workflows, context sources, MCP endpoints, and dashboards for .NET applications.

## License

MIT
