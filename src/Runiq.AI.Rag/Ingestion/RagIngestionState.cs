namespace Runiq.AI.Rag.Ingestion;

/// <summary>Stores successful ingestion fingerprints for the lifetime of the configured RAG service provider.</summary>
public sealed class RagIngestionState
{
    private readonly SemaphoreSlim persistenceGate = new(1, 1);
    private readonly HashSet<string> loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    internal object Gate { get; } = new();
    internal Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);
    internal sealed record Entry(string Hash, IReadOnlyList<string> ChunkIds);

    internal async ValueTask<IAsyncDisposable> AcquireAsync(string? path, CancellationToken cancellationToken)
    {
        await persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var normalizedPath = Normalize(path);
            if (normalizedPath is not null && loadedPaths.Add(normalizedPath) && File.Exists(normalizedPath))
            {
                var json = await File.ReadAllTextAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
                var stored = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(json);
                if (stored is not null) lock (Gate) foreach (var pair in stored) Entries[pair.Key] = new Entry(pair.Value.Hash, pair.Value.ChunkIds);
            }
            return new Lease(persistenceGate);
        }
        catch { persistenceGate.Release(); throw; }
    }

    internal async Task SaveAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalizedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(normalizedPath); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        Dictionary<string, PersistedEntry> copy; lock (Gate) copy = Entries.ToDictionary(pair => pair.Key, pair => new PersistedEntry(pair.Value.Hash, pair.Value.ChunkIds.ToArray()), StringComparer.Ordinal);
        var temporary = normalizedPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, System.Text.Json.JsonSerializer.Serialize(copy), cancellationToken).ConfigureAwait(false); File.Move(temporary, normalizedPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string? Normalize(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; }
    }

    private sealed record PersistedEntry(string Hash, IReadOnlyList<string> ChunkIds);
}
