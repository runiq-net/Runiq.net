using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Hosting;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Rag.Controllers;

/// <summary>
/// Exposes dashboard operations for inspecting and controlling registered RAG indexes.
/// </summary>
[ApiController]
[Route("api/rag/indexes")]
public sealed class RagManagementController : ControllerBase
{
    private readonly IRagIndexRegistry registry;
    private readonly IRagIngestionManager manager;
    private readonly ILogger<RagManagementController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagManagementController"/> class.
    /// </summary>
    /// <param name="registry">The registry containing configured RAG indexes.</param>
    /// <param name="manager">The manager responsible for ingestion operations.</param>
    /// <param name="logger">The logger used for ingestion management events.</param>
    public RagManagementController(
        IRagIndexRegistry registry,
        IRagIngestionManager manager,
        ILogger<RagManagementController> logger)
    {
        this.registry = registry;
        this.manager = manager;
        this.logger = logger;
    }

    /// <summary>
    /// Lists all registered RAG indexes and their current ingestion states.
    /// </summary>
    /// <returns>The registered RAG index summaries.</returns>
    [HttpGet]
    public IActionResult List() =>
        Ok(registry.GetMetadata()
            .Select(metadata => RagManagementMapper.MapListItem(metadata, manager.GetStatus(metadata.Name)))
            .ToArray());

    /// <summary>
    /// Gets the configuration and current state of one RAG index.
    /// </summary>
    /// <param name="indexName">The exact registered index name.</param>
    /// <returns>The index details, or a not-found response when it is not registered.</returns>
    [HttpGet("{indexName}")]
    public IActionResult Get(string indexName)
    {
        var metadata = Find(indexName);
        return metadata is null
            ? NotFoundError(indexName)
            : Ok(RagManagementMapper.MapDetail(metadata, manager.GetStatus(metadata.Name)));
    }

    /// <summary>
    /// Gets the current ingestion state of one RAG index.
    /// </summary>
    /// <param name="indexName">The exact registered index name.</param>
    /// <returns>The ingestion state, or a not-found response when the index is not registered.</returns>
    [HttpGet("{indexName}/status")]
    public IActionResult GetStatus(string indexName)
    {
        var metadata = Find(indexName);
        return metadata is null
            ? NotFoundError(indexName)
            : Ok(RagManagementMapper.Map(manager.GetStatus(metadata.Name)));
    }

    /// <summary>
    /// Starts a new ingestion operation for a registered RAG index.
    /// </summary>
    /// <param name="indexName">The exact registered index name.</param>
    /// <param name="cancellationToken">A token that cancels request processing.</param>
    /// <returns>The accepted operation, a conflict, or a not-found response.</returns>
    [HttpPost("{indexName}/ingestion/start")]
    public IActionResult Start(string indexName, CancellationToken cancellationToken)
    {
        var metadata = Find(indexName);
        if (metadata is null)
        {
            return NotFoundError(indexName);
        }

        logger.LogInformation("Manual RAG ingestion start requested for index {IndexName}.", metadata.Name);
        try
        {
            _ = manager.StartAsync(metadata.Name, cancellationToken);
            var status = manager.GetStatus(metadata.Name);
            var operation = status.ActiveOperation ?? status.LastOperation!;
            logger.LogInformation("Manual RAG ingestion start accepted for index {IndexName} as operation {OperationId}.", metadata.Name, operation.OperationId);
            return Accepted(value: RagManagementMapper.Map(operation));
        }
        catch (InvalidOperationException)
        {
            var active = manager.GetStatus(metadata.Name).ActiveOperation;
            logger.LogInformation("Manual RAG ingestion start conflicted for index {IndexName}; active operation {OperationId}.", metadata.Name, active?.OperationId);
            return Conflict(new RagManagementErrorDto("ActiveIngestionOperation", "The index already has an active ingestion operation.", RagManagementMapper.Map(active)));
        }
    }

    /// <summary>
    /// Cancels the active ingestion operation for a registered RAG index.
    /// </summary>
    /// <param name="indexName">The exact registered index name.</param>
    /// <param name="cancellationToken">A token that cancels request processing.</param>
    /// <returns>The cancelled operation, a conflict, or a not-found response.</returns>
    [HttpPost("{indexName}/ingestion/cancel")]
    public async Task<IActionResult> Cancel(string indexName, CancellationToken cancellationToken)
    {
        var metadata = Find(indexName);
        if (metadata is null)
        {
            return NotFoundError(indexName);
        }

        logger.LogInformation("RAG ingestion cancellation requested for index {IndexName}.", metadata.Name);
        if (manager.GetStatus(metadata.Name).ActiveOperation is null)
        {
            logger.LogInformation("RAG ingestion cancellation conflicted for index {IndexName} because no operation is active.", metadata.Name);
            return Conflict(new RagManagementErrorDto("NoActiveIngestionOperation", "The index has no active ingestion operation."));
        }

        await manager.CancelAsync(metadata.Name, cancellationToken).ConfigureAwait(false);
        var operation = manager.GetStatus(metadata.Name).LastOperation;
        logger.LogInformation("RAG ingestion cancellation accepted for index {IndexName}; operation {OperationId} is {OperationState}.", metadata.Name, operation?.OperationId, operation?.State);
        return Ok(RagManagementMapper.Map(operation));
    }

    private RagIndexMetadata? Find(string indexName) =>
        registry.GetMetadata().SingleOrDefault(metadata =>
            string.Equals(metadata.Name, indexName, StringComparison.Ordinal));

    private ObjectResult NotFoundError(string indexName) =>
        NotFound(new RagManagementErrorDto("RagIndexNotFound", $"RAG index '{indexName}' is not registered."));
}
