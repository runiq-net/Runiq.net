using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Controllers;

/// <summary>
/// Exposes read-only dashboard metadata for registered agents and tools.
/// </summary>
[ApiController]
[Route("metadata")]
public sealed class MetadataController : ControllerBase
{
    private readonly IRuntimeMetadataService metadataService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataController"/> class.
    /// </summary>
    /// <param name="metadataService">The runtime metadata reader.</param>
    public MetadataController(IRuntimeMetadataService metadataService)
    {
        this.metadataService = metadataService;
    }

    /// <summary>
    /// Gets metadata for all registered agents.
    /// </summary>
    /// <returns>The registered agent metadata.</returns>
    [HttpGet("agents")]
    public IActionResult GetAgents() => Ok(metadataService.GetAgents());

    /// <summary>
    /// Gets metadata for all registered tools.
    /// </summary>
    /// <returns>The registered tool metadata.</returns>
    [HttpGet("tools")]
    public IActionResult GetTools() => Ok(metadataService.GetTools());
}
