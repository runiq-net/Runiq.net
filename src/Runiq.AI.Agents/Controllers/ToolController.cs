using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Core.Tools;

namespace Runiq.AI.Agents.Controllers;

/// <summary>
/// Exposes dashboard tool playground operations.
/// </summary>
[ApiController]
[Route("api/tools")]
public sealed class ToolController : ControllerBase
{
    private readonly ToolRunApiHandler handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolController"/> class.
    /// </summary>
    /// <param name="handler">The registered tool invocation handler.</param>
    public ToolController(ToolRunApiHandler handler)
    {
        this.handler = handler;
    }

    /// <summary>
    /// Runs a registered tool with the supplied input.
    /// </summary>
    /// <param name="toolName">The registered tool name.</param>
    /// <param name="request">The optional tool input payload.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The tool invocation result or an HTTP error result.</returns>
    [HttpPost("{toolName}/run")]
    public Task<IResult> Run(
        string toolName,
        [FromBody] ToolRunRequest? request,
        CancellationToken cancellationToken) =>
        handler.RunAsync(toolName, request, cancellationToken);
}
