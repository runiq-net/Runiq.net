using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Agents.Providers.OpenAI;

/// <summary>Invokes the OpenAI embeddings endpoint through the provider-neutral embedding contract.</summary>
internal sealed class OpenAIEmbeddingClient : IEmbeddingClient
{
    internal const string HttpClientName = "Runiq.OpenAI.Embeddings";
    private readonly HttpClient httpClient;
    private readonly string apiKey;

    /// <summary>Initializes an OpenAI embedding client.</summary>
    /// <param name="httpClient">The HTTP client whose base address points to the OpenAI API.</param>
    /// <param name="apiKey">The API key sent as a bearer credential.</param>
    internal OpenAIEmbeddingClient(HttpClient httpClient, string? apiKey)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.apiKey = string.IsNullOrWhiteSpace(apiKey)
            ? throw new ArgumentException("An OpenAI API key is required.", nameof(apiKey))
            : apiKey;
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = JsonContent.Create(new
        {
            model = request.Model.ModelName,
            input = request.Inputs,
            dimensions = request.Dimensions,
        });

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var results = payload.RootElement.GetProperty("data")
            .EnumerateArray()
            .OrderBy(item => item.GetProperty("index").GetInt32())
            .Select(item =>
            {
                var index = item.GetProperty("index").GetInt32();
                var vector = item.GetProperty("embedding").EnumerateArray().Select(value => value.GetSingle()).ToArray();
                return new EmbeddingResult(index, vector, vector.Length);
            })
            .ToArray();

        if (results.Length != request.Inputs.Count)
        {
            throw new InvalidOperationException("OpenAI returned an unexpected number of embeddings.");
        }

        return new EmbeddingResponse(results);
    }
}
