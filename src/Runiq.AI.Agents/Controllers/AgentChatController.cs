using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Core.Agents;

namespace Runiq.AI.Agents.Controllers;

/// <summary>
/// Exposes dashboard agent chat operations.
/// </summary>
[ApiController]
[Route("api/agents")]
public sealed class AgentChatController : ControllerBase
{
    private readonly AgentChatApiHandler handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentChatController"/> class.
    /// </summary>
    /// <param name="handler">The agent chat application handler.</param>
    public AgentChatController(AgentChatApiHandler handler)
    {
        this.handler = handler;
    }

    /// <summary>
    /// Executes an agent chat request as a result or streaming response.
    /// </summary>
    /// <param name="agentId">The registered agent identifier.</param>
    /// <param name="request">The chat request.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The agent response or an HTTP error result.</returns>
    [HttpPost("{agentId}/chat")]
    public Task<IResult> Chat(
        string agentId,
        [FromBody] AgentChatRequest request,
        CancellationToken cancellationToken) =>
        handler.ChatAsync(agentId, request, HttpContext, cancellationToken);
}
