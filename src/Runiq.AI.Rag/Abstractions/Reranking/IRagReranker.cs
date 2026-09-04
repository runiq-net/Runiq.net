using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Rag.Abstractions.Reranking;

/// <summary>Reevaluates accepted retrieval candidates for query relevance and aggregate answerability.</summary>
public interface IRagReranker
{
    /// <summary>Reranks the bounded candidates in the request.</summary>
    /// <param name="request">The provider-neutral query and candidate request.</param>
    /// <param name="cancellationToken">A token that propagates caller cancellation or the runtime timeout.</param>
    /// <returns>A complete reranking result for every request candidate.</returns>
    Task<RagRerankResult> RerankAsync(
        RagRerankRequest request,
        CancellationToken cancellationToken = default);
}
