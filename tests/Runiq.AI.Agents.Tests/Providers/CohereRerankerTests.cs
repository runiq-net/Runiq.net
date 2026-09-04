using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Providers.Cohere;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class CohereRerankerTests
{
    // Proves the production adapter sends the v2 contract and maps indices, scores, and threshold answerability.
    [Fact]
    public async Task RerankAsync_ValidResponse_MapsCompleteResult()
    {
        var handler = new RecordingHandler("""
            {"results":[{"index":1,"relevance_score":0.91},{"index":0,"relevance_score":0.2}]}
            """);
        var client = CreateClient(handler, threshold: 0.7);
        var request = new RagRerankRequest("question",
        [
            new("doc-a", "chunk-a", "first content", 1),
            new("doc-b", "chunk-b", "second content", 2),
        ]);

        var result = await client.RerankAsync(request);

        Assert.Equal(RagAnswerability.Answerable, result.Answerability);
        Assert.Equal(["chunk-b", "chunk-a"], result.Candidates.Select(item => item.ChunkId));
        Assert.Equal(RagAnswerability.Answerable, result.Candidates[0].Answerability);
        Assert.Equal(RagAnswerability.NotAnswerable, result.Candidates[1].Answerability);
        Assert.Equal("Bearer test-key", handler.Request!.Headers.Authorization?.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("rerank-v4.0-fast", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("top_n").GetInt32());
        Assert.Equal("second content", body.RootElement.GetProperty("documents")[1].GetString());
    }

    // Proves provider error bodies and credentials are not copied into the exception consumed by runtime observability.
    [Fact]
    public async Task RerankAsync_ProviderFailure_ThrowsSafeStatusOnlyException()
    {
        var handler = new RecordingHandler("provider-secret-detail", HttpStatusCode.TooManyRequests);
        var client = CreateClient(handler);
        var request = new RagRerankRequest("question", [new("doc", "chunk", "content", 1)]);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.RerankAsync(request));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.DoesNotContain("provider-secret-detail", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", exception.Message, StringComparison.Ordinal);
    }

    private static CohereReranker CreateClient(RecordingHandler handler, double threshold = 0.5) =>
        new(new HttpClient(handler), Options.Create(new CohereRerankerOptions
        {
            ApiKey = "test-key",
            MinimumAnswerableRelevance = threshold,
        }));

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
