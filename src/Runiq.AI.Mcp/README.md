# Runiq.AI.Mcp

![NuGet Version](https://img.shields.io/nuget/v/Runiq.AI.Mcp?label=nuget)

MCP server integration for ASP.NET Core applications.

`Runiq.AI.Mcp` lets an ASP.NET Core application expose its application services as MCP-compatible tools. Use it to turn existing C# services into tools that can be called by MCP-compatible clients without rewriting your application around a separate runtime.

## Why Runiq.AI.Mcp?

`Runiq.AI.Mcp` is designed for .NET developers who want to expose application capabilities through the Model Context Protocol while staying inside the ASP.NET Core hosting and dependency injection model.

It focuses on:

- ASP.NET Core native MCP server hosting
- Dependency-injection friendly MCP tools
- Existing application service reuse
- Minimal endpoint mapping
- Streamable HTTP transport support
- Simple integration with existing ASP.NET Core applications

## Install

```powershell
dotnet add package Runiq.AI.Mcp --version 1.0.0
```

## Quick Start

Register MCP services:

```csharp
builder.Services.AddRuniqMcp();
```

Map the MCP endpoint:

```csharp
var app = builder.Build();

app.MapRuniqMcp();

app.Run();
```

By default, the MCP endpoint is available at:

```text
/mcp
```

## Custom Endpoint Path

You can expose the MCP endpoint under a custom path:

```csharp
app.MapRuniqMcp("/ai/mcp");
```

Example endpoint paths:

```text
/mcp
/ai/mcp
/dashboard/api/mcp
```

## Create an MCP Tool

MCP tools can use regular ASP.NET Core dependency injection services.

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public sealed class OrderStatusMcpTool
{
    private readonly IOrderService _orders;

    public OrderStatusMcpTool(IOrderService orders)
    {
        _orders = orders;
    }

    [McpServerTool]
    [Description("Gets the current status of an order.")]
    public Task<OrderStatusResult> GetOrderStatus(
        [Description("The order identifier.")] string orderId)
    {
        return _orders.GetStatusAsync(orderId);
    }
}
```

Register your application service as usual:

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
```

Then register and map Runiq MCP:

```csharp
builder.Services.AddRuniqMcp();

var app = builder.Build();

app.MapRuniqMcp();

app.Run();
```

## Minimal Example

```csharp
using ModelContextProtocol.Server;
using Runiq.AI.Mcp;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddRuniqMcp();

var app = builder.Build();

app.MapRuniqMcp();

app.Run();

public interface IOrderService
{
    Task<OrderStatusResult> GetStatusAsync(string orderId);
}

public sealed class OrderService : IOrderService
{
    public Task<OrderStatusResult> GetStatusAsync(string orderId)
    {
        return Task.FromResult(
            new OrderStatusResult(orderId, "Processing"));
    }
}

public sealed record OrderStatusResult(string OrderId, string Status);

[McpServerToolType]
public sealed class OrderStatusMcpTool
{
    private readonly IOrderService _orders;

    public OrderStatusMcpTool(IOrderService orders)
    {
        _orders = orders;
    }

    [McpServerTool]
    [Description("Gets the current status of an order.")]
    public Task<OrderStatusResult> GetOrderStatus(
        [Description("The order identifier.")] string orderId)
    {
        return _orders.GetStatusAsync(orderId);
    }
}
```

## What This Enables

`Runiq.AI.Mcp` allows your application services to become MCP tools.

```text
ASP.NET Core service
        ↓
Runiq.AI.Mcp
        ↓
MCP-compatible tool
        ↓
MCP client
```

This means you can expose existing business capabilities such as:

- Order lookup
- Customer support actions
- Travel planning services
- Internal automation services
- Reporting and analytics functions
- Domain-specific application tools

without moving those capabilities out of your ASP.NET Core application.

## Typical Use Cases

Use `Runiq.AI.Mcp` when you want to:

- Add an MCP server endpoint to an ASP.NET Core app
- Expose existing C# services as MCP tools
- Reuse ASP.NET Core dependency injection
- Keep tool implementation close to application logic
- Connect your application to MCP-compatible clients
- Build AI-accessible APIs without creating a separate tool server

## Current Scope

The current release focuses on MCP tools.

Planned areas:

- MCP resources
- MCP prompts
- Runiq native tool exposure
- MCP client integration
- Dashboard visibility for MCP endpoint status

## Related Packages

Runiq AI is modular. `Runiq.AI.Mcp` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Core` | Hosts agents and the embedded dashboard in ASP.NET Core |
| `Runiq.AI.Agents` | Defines agents, tools, provider integration, and streaming execution primitives |
| `Runiq.AI.Workflows` | Provides code-first workflow orchestration primitives |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

This package targets the Runiq AI 1.0.0 stable release.

The main direction is clear:

> Expose ASP.NET Core services as MCP-compatible tools and connect them with the broader Runiq AI agent runtime.

## License

MIT