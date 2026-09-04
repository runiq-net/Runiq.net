using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Core.Rag;

namespace Runiq.AI.Core.Controllers;

/// <summary>
/// Exposes read-only dashboard information about the configured RAG services.
/// </summary>
[ApiController]
[Route("api/rag")]
public sealed class RuniqRagController : ControllerBase
{
    private readonly IRuniqRagInfoProvider infoProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuniqRagController"/> class.
    /// </summary>
    /// <param name="infoProvider">The provider that reads current RAG visibility information.</param>
    public RuniqRagController(IRuniqRagInfoProvider infoProvider)
    {
        this.infoProvider = infoProvider;
    }

    /// <summary>
    /// Gets the current RAG configuration visibility information.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The current RAG configuration information.</returns>
    [HttpGet]
    public async Task<ActionResult<RuniqRagInfo>> GetInfo(CancellationToken cancellationToken) =>
        Ok(await infoProvider.GetInfoAsync(cancellationToken));
}
