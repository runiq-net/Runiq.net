using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Agents.Providers.Cohere;

/// <summary>Reranks accepted RAG candidates with Cohere's cross-encoder Rerank v2 API.</summary>
public sealed class CohereReranker : IRagReranker
{
    private static readonly Uri Endpoint = new("https://api.cohere.com/v2/rerank");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly CohereRerankerOptions options;

    /// <summary>Initializes the Cohere reranker with its managed HTTP client and validated options.</summary>
    public CohereReranker(HttpClient httpClient, IOptions<CohereRerankerOptions> options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<RagRerankResult> RerankAsync(
        RagRerankRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Candidates.Count == 0) return new([], RagAnswerability.Unknown);

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        if (!string.IsNullOrWhiteSpace(options.ClientName)) message.Headers.Add("X-Client-Name", options.ClientName);
        message.Content = JsonContent.Create(new CohereRerankRequest(
            options.Model,
            request.Query,
            request.Candidates.Select(candidate => candidate.Content).ToArray(),
            request.Candidates.Count), options: JsonOptions);

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Cohere reranking failed with status code {(int)response.StatusCode}.", null, response.StatusCode);

        CohereRerankResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<CohereRerankResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Cohere returned malformed reranking JSON.", exception);
        }

        if (payload?.Results is null || payload.Results.Count != request.Candidates.Count)
            throw new InvalidDataException("Cohere did not return one reranking result for every candidate.");

        var seen = new HashSet<int>();
        var candidates = payload.Results.Select(result =>
        {
            if (result.Index < 0 || result.Index >= request.Candidates.Count || !seen.Add(result.Index))
                throw new InvalidDataException("Cohere returned an invalid candidate index.");
            if (!double.IsFinite(result.RelevanceScore) || result.RelevanceScore is < 0 or > 1)
                throw new InvalidDataException("Cohere returned an invalid relevance score.");
            var source = request.Candidates[result.Index];
            return new RagRerankCandidateResult(
                source.DocumentId,
                source.ChunkId,
                result.RelevanceScore,
                ToAnswerability(result.RelevanceScore));
        }).ToArray();

        var aggregate = candidates.Any(candidate => candidate.Answerability == RagAnswerability.Answerable)
            ? RagAnswerability.Answerable
            : RagAnswerability.NotAnswerable;
        return new(candidates, aggregate);
    }

    private RagAnswerability ToAnswerability(double relevance) =>
        relevance >= options.MinimumAnswerableRelevance
            ? RagAnswerability.Answerable
            : RagAnswerability.NotAnswerable;

    private sealed record CohereRerankRequest(
        string Model,
        string Query,
        IReadOnlyList<string> Documents,
        [property: JsonPropertyName("top_n")] int TopN);

    private sealed record CohereRerankResponse(IReadOnlyList<CohereRerankResult>? Results);
    private sealed record CohereRerankResult(int Index, [property: JsonPropertyName("relevance_score")] double RelevanceScore);
}
