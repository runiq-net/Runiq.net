using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Runiq.AI.Mcp;

/// <summary>Provides endpoint registration for the Runiq MCP transport.</summary>
public static class RuniqMcpEndpointRouteBuilderExtensions
{
    /// <summary>Maps the MCP transport at the requested route pattern.</summary>
    /// <param name="endpoints">Endpoint route builder receiving the MCP mapping.</param>
    /// <param name="pattern">Route pattern used by MCP clients.</param>
    /// <returns>The same endpoint route builder for fluent configuration.</returns>
    public static IEndpointRouteBuilder MapRuniqMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        endpoints.MapMcp(pattern);

        return endpoints;
    }
}
