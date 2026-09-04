using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Controllers;

/// <summary>Exposes the sample dashboard redirect and retrieval demonstration endpoints.</summary>
[ApiController]
public sealed class RetrievalDemoController : ControllerBase
{
    /// <summary>Redirects the sample root to the dashboard.</summary>
    [HttpGet("/")]
    public IActionResult Index() => Redirect("/dashboard");

    /// <summary>Executes a sample retrieval query using the requested retrieval mode.</summary>
    /// <param name="mode">Semantic, lexical, or hybrid retrieval mode.</param>
    /// <param name="query">Optional query text.</param>
    /// <param name="retriever">Registered RAG retriever.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The retrieval statistics and ranked candidates.</returns>
    [HttpGet("/retrieval-demo/{mode}")]
    public async Task<IActionResult> RetrieveAsync(
        string mode,
        string? query,
        [FromServices] IRagRetriever retriever,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RagRetrievalMode>(mode, ignoreCase: true, out var retrievalMode) || !Enum.IsDefined(retrievalMode))
            return BadRequest("Mode must be Semantic, Lexical, or Hybrid.");

        var effectiveQuery = string.IsNullOrWhiteSpace(query) ? "IRagRetriever" : query;
        var result = await retriever.RetrieveWithMetadataAsync(new RagQuery
        {
            IndexName = CorporateDocumentAssistantSetup.IndexName,
            Text = effectiveQuery,
            TopK = 5,
            Mode = retrievalMode,
        }, cancellationToken);

        return Ok(new
        {
            retrievalMode,
            query = effectiveQuery,
            result.Statistics,
            candidates = result.Candidates.Select(candidate => new
            {
                candidate.Chunk.DocumentId,
                candidate.Chunk.Id,
                candidate.RawScore,
                candidate.Relevance,
                candidate.Metric,
                candidate.HigherIsBetter,
                candidate.Provenance,
            }),
        });
    }
}
