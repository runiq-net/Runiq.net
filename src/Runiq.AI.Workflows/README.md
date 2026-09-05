# Runiq.AI.Workflows

![NuGet Version](https://img.shields.io/nuget/v/Runiq.AI.Workflows?label=nuget)

Code-first workflow orchestration primitives for Runiq AI.

`Runiq.AI.Workflows` lets you compose agents into repeatable code-first flows. Use it to define workflow steps, model success and failure transitions, validate flow definitions, and expose workflow metadata and execution results through the Runiq AI runtime and dashboard.

## Why Runiq.AI.Workflows?

Single agents are useful, but real application scenarios often require multiple agents or processing steps to work together.

`Runiq.AI.Workflows` helps you model these flows in C#.

It focuses on:

- Code-first workflow definitions
- Agent-based workflow steps
- Explicit success transitions
- Explicit failure handling
- Flow validation
- Dashboard-visible workflow metadata
- Runtime-friendly execution result models

## Install

```powershell
dotnet add package Runiq.AI.Workflows --version 1.0.0
```

## Create a Workflow

```csharp
using Runiq.AI.Workflows;
using Runiq.AI.Workflows.Domain;

var flow = new Flow(
        id: "travel-planning-workflow",
        name: "Travel Planning Workflow")
    .Step<WeatherAgent>("weather")
        .OnSuccess("places")
        .OnFailureContinue("places")
    .Step<PlacesAgent>("places")
        .OnSuccess("planner")
        .OnFailureContinue("planner")
    .Step<PlannerAgent>("planner")
        .OnFailureStop()
    .Build();
```

A workflow is made of named steps.

Each step can define what happens when it succeeds or fails.

## Register Workflows

Register workflows during application startup:

```csharp
builder.Services.AddRuniqWorkflows(options =>
{
    options.AddFlow(flow);
});
```

Registered workflows can be surfaced through the Runiq AI runtime and dashboard.

## Step Transitions

Workflow steps can explicitly define their next action.

```csharp
.Step<WeatherAgent>("weather")
    .OnSuccess("places")
    .OnFailureContinue("places")
```

This means:

- If the `weather` step succeeds, continue with the `places` step
- If the `weather` step fails, continue with the `places` step anyway

For critical steps, failure can stop the workflow:

```csharp
.Step<PlannerAgent>("planner")
    .OnFailureStop()
```

This makes failure behavior visible in the workflow definition instead of hiding it inside application code.

## Basic Travel Planning Flow

Example flow:

```text
weather
  ↓
places
  ↓
planner
```

In C#:

```csharp
var flow = new Flow(
        id: "travel-planning-workflow",
        name: "Travel Planning Workflow")
    .Step<WeatherAgent>("weather")
        .OnSuccess("places")
        .OnFailureContinue("places")
    .Step<PlacesAgent>("places")
        .OnSuccess("planner")
        .OnFailureContinue("planner")
    .Step<PlannerAgent>("planner")
        .OnFailureStop()
    .Build();
```

This kind of flow is useful when an application needs several agents to contribute to a final result.

## Failure Handling

`Runiq.AI.Workflows` supports explicit failure behavior.

| Behavior | Purpose |
|---|---|
| `OnFailureContinue(...)` | Continue to another step even if the current step fails |
| `OnFailureStop()` | Stop the workflow when the current step fails |

This keeps workflow behavior understandable, testable, and dashboard-visible.

## Typical Use Cases

Use `Runiq.AI.Workflows` when you want to:

- Chain multiple agents into repeatable processes
- Model success and failure transitions explicitly
- Build multi-step AI workflows
- Keep workflow definitions in C#
- Expose workflow metadata to the dashboard
- Make workflow execution easier to inspect
- Keep orchestration logic close to application behavior

## Example Scenarios

| Scenario | Workflow Example |
|---|---|
| Travel planning | Weather agent → Places agent → Planner agent |
| Expense analysis | Search agent → Aggregation agent → Report agent |
| Support automation | Classifier agent → Knowledge agent → Response agent |
| Content generation | Research agent → Draft agent → Review agent |
| Internal operations | Data collection agent → Validation agent → Action agent |

## Dashboard Visibility

When used with `Runiq.AI.Core`, workflow definitions and execution metadata can be exposed through the embedded dashboard.

This helps developers inspect:

- Registered workflows
- Workflow steps
- Step order
- Success transitions
- Failure behavior
- Execution results

## Related Packages

Runiq AI is modular. `Runiq.AI.Workflows` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Agents` | Provides the agent runtime primitives used by workflow steps |
| `Runiq.AI.Core` | Hosts workflow runtime and dashboard endpoints |
| `Runiq.AI.Mcp` | Exposes ASP.NET Core applications through MCP-compatible tools |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

This package targets the Runiq AI 1.0.0 stable release.

The main direction is clear:

> Build code-first AI agents, tools, workflows, context sources, MCP endpoints, and dashboards for .NET applications.

## License

MIT