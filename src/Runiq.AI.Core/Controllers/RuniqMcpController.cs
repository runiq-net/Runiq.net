using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Runiq.AI.Core.Mcp;

namespace Runiq.AI.Core.Controllers;

/// <summary>
/// Exposes dashboard endpoints for MCP discovery and tool execution.
/// </summary>
[ApiController]
[Route("api/mcp")]
public sealed class RuniqMcpController : ControllerBase
{
    private readonly IEnumerable<EndpointDataSource> endpointDataSources;
    private readonly RuniqMcpToolRunApiHandler toolRunHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuniqMcpController"/> class.
    /// </summary>
    /// <param name="endpointDataSources">The endpoint sources used to discover the MCP transport endpoint.</param>
    /// <param name="toolRunHandler">The handler that validates and invokes dashboard MCP tool requests.</param>
    public RuniqMcpController(
        IEnumerable<EndpointDataSource> endpointDataSources,
        RuniqMcpToolRunApiHandler toolRunHandler)
    {
        this.endpointDataSources = endpointDataSources;
        this.toolRunHandler = toolRunHandler;
    }

    /// <summary>
    /// Gets the currently exposed MCP transport and tool metadata.
    /// </summary>
    /// <returns>The MCP visibility information for the current host.</returns>
    [HttpGet]
    public ActionResult<RuniqMcpInfo> GetInfo() =>
        Ok(RuniqMcpInfoReader.Read(Request, endpointDataSources));

    /// <summary>
    /// Runs an MCP tool with the supplied dashboard input.
    /// </summary>
    /// <param name="toolName">The registered MCP tool name.</param>
    /// <param name="request">The optional tool input payload.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The tool invocation result or an HTTP error result.</returns>
    [HttpPost("tools/{toolName}/run")]
    public Task<IResult> RunTool(
        string toolName,
        [FromBody] RuniqMcpToolRunRequest? request,
        CancellationToken cancellationToken) =>
        toolRunHandler.RunAsync(toolName, request, cancellationToken);
}
