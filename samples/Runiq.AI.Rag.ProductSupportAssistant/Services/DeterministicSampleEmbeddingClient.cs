using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Rag.ProductSupportAssistant.Services;

/// <summary>
/// Provides local deterministic embeddings so the sample can ingest documents without provider credentials.
/// </summary>
public sealed class DeterministicSampleEmbeddingClient : IEmbeddingClient
{
    /// <summary>
    /// Gets the fixed embedding dimension count used by the sample index.
    /// </summary>
    public const int Dimensions = 8;

    /// <inheritdoc />
    public Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var results = request.Inputs
            .Select((input, index) => new EmbeddingResult(index, CreateVector(input), Dimensions))
            .ToArray();

        return Task.FromResult(new EmbeddingResponse(results));
    }

    private static IReadOnlyList<float> CreateVector(string input)
    {
        var vector = new float[Dimensions];

        for (var index = 0; index < input.Length; index++)
        {
            vector[index % Dimensions] += ((input[index] * (index + 1)) % 997) / 997.0f;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = MathF.Round(vector[index], 6);
        }

        return vector;
    }
}
