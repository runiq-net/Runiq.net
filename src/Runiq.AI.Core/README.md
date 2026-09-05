# Runiq.AI.Core

![NuGet Version](https://img.shields.io/nuget/v/Runiq.AI.Core?label=nuget)

Embedded ASP.NET Core runtime and dashboard hosting layer for Runiq AI.

`Runiq.AI.Core` wires Runiq AI into ASP.NET Core applications. Use it to register agents, expose runtime endpoints, host the embedded dashboard, browse context spaces, and inspect workflow activity from your own application.

## Why Runiq.AI.Core?

`Runiq.AI.Core` is the ASP.NET Core integration package for Runiq AI.

It focuses on:

- ASP.NET Core service registration
- Agent runtime hosting
- Embedded dashboard hosting
- Runtime metadata endpoints
- Agent metadata endpoints
- Context space browsing endpoints
- Workflow dashboard endpoint integration
- Application-owned hosting with no separate dashboard server

## Install

```powershell
dotnet add package Runiq.AI.Core --version 1.0.0
```

## Quick Start

Register Runiq AI services:

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

Host the embedded dashboard:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
});
```

Start your ASP.NET Core application and open:

```text
/dashboard
```

The dashboard is hosted inside your own application. No separate dashboard service is required.

## Minimal Example

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
        id: "assistant",
        name: "Assistant",
        instructions: "You are a helpful assistant.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"]));
});

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
});

app.Run();
```

## Dashboard Hosting

`UseRuniqDashboard` serves the embedded dashboard UI from your ASP.NET Core application.

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "My Runiq App";
});
```

Typical dashboard path examples:

```text
/dashboard
/admin/runiq
/runiq
```

## What It Provides

`Runiq.AI.Core` provides the runtime and hosting layer for the broader Runiq AI platform.

| Area | Description |
|---|---|
| Service registration | Registers Runiq runtime services in ASP.NET Core |
| Agent metadata | Exposes registered agent information |
| Dashboard hosting | Serves the embedded dashboard UI |
| Runtime endpoints | Provides endpoints used by the dashboard and runtime |
| Context browsing | Surfaces context spaces, sources, and skill documents |
| Workflow visibility | Provides workflow dashboard endpoint integration |

## Application-Owned Runtime

Runiq AI is designed to run inside your application.

That means:

- Your ASP.NET Core app owns the hosting process
- The dashboard is embedded into your app
- Agents are registered from your application startup
- Context spaces and workflows are exposed through your runtime
- You do not need to deploy a separate Runiq dashboard server

## Typical Use Cases

Use `Runiq.AI.Core` when you want to:

- Add Runiq AI to an ASP.NET Core application
- Host the embedded dashboard
- Register and inspect agents
- Expose runtime metadata endpoints
- Browse context spaces and source documents
- Inspect workflow definitions and activity
- Build internal AI agent platforms for .NET applications

## Related Packages

Runiq AI is modular. `Runiq.AI.Core` is usually used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Agents` | Defines agents, tools, provider integration, and streaming execution primitives |
| `Runiq.AI.Workflows` | Provides code-first workflow orchestration primitives |
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