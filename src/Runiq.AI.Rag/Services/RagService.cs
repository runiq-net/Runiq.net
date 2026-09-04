using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Ingestion;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Chunking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.VectorStores;

namespace Runiq.AI.Rag.Services;

/// <summary>
/// Provides the default high-level RAG service orchestration.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IRagRetriever retriever;
    private readonly IRagDocumentIngestionService documentIngestion;
    private readonly IRagVectorStoreUpsertPipeline upsertPipeline;
    private readonly IRagVectorStore vectorStore;
    private readonly RagIngestionState state;
    private readonly RagIngestionOptions ingestionOptions;
    private readonly IRagIndexRegistry? indexRegistry;
    private readonly IRagIndexRuntimeConfigurationResolver? runtimeResolver;
    private readonly IRagEmbeddingInputPreparer? inputPreparer;
    private readonly IRagUpsertVectorRequestMapper? requestMapper;
    private readonly IRagVectorRecordDimensionValidator? dimensionValidator;
    private readonly IRagOperationTelemetryRecorder? telemetryRecorder;
    private readonly RagOptions ragOptions;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> documentLocks = new(StringComparer.Ordinal);

    /// <summary>Initializes a retrieval-only facade for compatibility with direct construction.</summary>
    /// <param name="retriever">The retriever used to retrieve relevant RAG search results.</param>
    public RagService(IRagRetriever retriever)
    {
        this.retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        documentIngestion = null!; upsertPipeline = null!; vectorStore = null!; state = null!; ingestionOptions = new RagIngestionOptions(); ragOptions = new RagOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RagService"/> class.
    /// </summary>
    /// <param name="retriever">The retriever used to retrieve relevant RAG search results.</param>
    /// <param name="documentIngestion">The existing document chunking and embedding pipeline.</param>
    /// <param name="upsertPipeline">The vector-store persistence pipeline.</param>
    /// <param name="vectorStore">The vector store used to create indexes and remove stale chunks.</param>
    /// <param name="state">The ingestion fingerprint state scoped to the service provider.</param>
    /// <param name="ingestionOptions">The ingestion configuration.</param>
    /// <param name="indexRegistry">The optional registered-index source of truth.</param>
    /// <param name="runtimeResolver">The optional per-index runtime dependency resolver.</param>
    /// <param name="inputPreparer">The embedding input preparer used by effective index pipelines.</param>
    /// <param name="requestMapper">The vector request mapper used by effective index pipelines.</param>
    /// <param name="dimensionValidator">The vector dimension validator used by effective index pipelines.</param>
    /// <param name="telemetryRecorder">The optional observational upsert telemetry recorder.</param>
    /// <param name="ragOptions">The global defaults retained for legacy unregistered-index calls.</param>
    public RagService(IRagRetriever retriever, IRagDocumentIngestionService documentIngestion, IRagVectorStoreUpsertPipeline upsertPipeline, IRagVectorStore vectorStore, RagIngestionState state, IOptions<RagIngestionOptions>? ingestionOptions = null, IRagIndexRegistry? indexRegistry = null, IRagIndexRuntimeConfigurationResolver? runtimeResolver = null, IRagEmbeddingInputPreparer? inputPreparer = null, IRagUpsertVectorRequestMapper? requestMapper = null, IRagVectorRecordDimensionValidator? dimensionValidator = null, IRagOperationTelemetryRecorder? telemetryRecorder = null, IOptions<RagOptions>? ragOptions = null)
    {
        this.retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        this.documentIngestion = documentIngestion ?? throw new ArgumentNullException(nameof(documentIngestion));
        this.upsertPipeline = upsertPipeline ?? throw new ArgumentNullException(nameof(upsertPipeline));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.ingestionOptions = ingestionOptions?.Value ?? new RagIngestionOptions();
        this.indexRegistry = indexRegistry;
        this.runtimeResolver = runtimeResolver;
        this.inputPreparer = inputPreparer;
        this.requestMapper = requestMapper;
        this.dimensionValidator = dimensionValidator;
        this.telemetryRecorder = telemetryRecorder;
        this.ragOptions = ragOptions?.Value ?? new RagOptions();
    }

    /// <inheritdoc />
    public async Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Validate(indexName);
        var pipeline = ResolveIngestionPipeline(indexName);
        await using var stateLease = await state.AcquireAsync(ingestionOptions.StatePath, cancellationToken).ConfigureAwait(false);
        var started = TimeProvider.System.GetTimestamp();
        var documents = await source.GetDocumentsAsync(cancellationToken).ConfigureAwait(false);
        var failures = new List<RagIngestionFailure>(); var created = 0; var updated = 0; var skipped = 0; var chunks = 0;
        var sourceKey = source.GetType().FullName ?? source.GetType().Name;
        if (ingestionOptions.MaxConcurrency <= 0) throw new InvalidOperationException("RAG ingestion MaxConcurrency must be greater than zero.");
        await Parallel.ForEachAsync(documents.OrderBy(item => item.Id, StringComparer.Ordinal), new ParallelOptions { MaxDegreeOfParallelism = ingestionOptions.MaxConcurrency, CancellationToken = cancellationToken }, async (document, token) =>
        {
            try { var outcome = await IngestCoreAsync(document, indexName, sourceKey, pipeline.DocumentIngestion, pipeline.UpsertPipeline, pipeline.VectorStore, token).ConfigureAwait(false); lock (failures) { created += outcome.Created; updated += outcome.Updated; skipped += outcome.Skipped; chunks += outcome.Chunks; } }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { lock (failures) failures.Add(new RagIngestionFailure { DocumentId = document.Id, Message = exception.Message }); if (ingestionOptions.FailFast) throw; }
        }).ConfigureAwait(false);
        var deleted = ingestionOptions.PropagateDeletes ? await PropagateDeletesAsync(sourceKey, indexName, documents.Select(x => x.Id), pipeline.VectorStore, cancellationToken).ConfigureAwait(false) : 0;
        await state.SaveAsync(ingestionOptions.StatePath, cancellationToken).ConfigureAwait(false);
        return new RagIngestionReport { DiscoveredDocuments = documents.Count, CreatedDocuments = created, UpdatedDocuments = updated, SkippedDocuments = skipped, DeletedDocuments = deleted, FailedDocuments = failures.Count, CreatedChunks = chunks, Failures = failures, Duration = TimeProvider.System.GetElapsedTime(started) };
    }

    /// <inheritdoc />
    public async Task<RagIngestionReport> IngestAsync(RagSourceDocument document, string indexName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document); Validate(indexName); var started = TimeProvider.System.GetTimestamp();
        await using var stateLease = await state.AcquireAsync(ingestionOptions.StatePath, cancellationToken).ConfigureAwait(false);
        var pipeline = ResolveIngestionPipeline(indexName);
        try { var outcome = await IngestCoreAsync(document, indexName, "application", pipeline.DocumentIngestion, pipeline.UpsertPipeline, pipeline.VectorStore, cancellationToken).ConfigureAwait(false); await state.SaveAsync(ingestionOptions.StatePath, cancellationToken).ConfigureAwait(false); return new RagIngestionReport { DiscoveredDocuments = 1, CreatedDocuments = outcome.Created, UpdatedDocuments = outcome.Updated, SkippedDocuments = outcome.Skipped, CreatedChunks = outcome.Chunks, Duration = TimeProvider.System.GetElapsedTime(started) }; }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { return new RagIngestionReport { DiscoveredDocuments = 1, FailedDocuments = 1, Failures = [new RagIngestionFailure { DocumentId = document.Id, Message = exception.Message }], Duration = TimeProvider.System.GetElapsedTime(started) }; }
    }

    private async Task<(int Created, int Updated, int Skipped, int Chunks)> IngestCoreAsync(RagSourceDocument source, string indexName, string sourceKey, IRagDocumentIngestionService effectiveDocumentIngestion, IRagVectorStoreUpsertPipeline effectiveUpsertPipeline, IRagVectorStore effectiveVectorStore, CancellationToken token)
    {
        var documentLock = documentLocks.GetOrAdd(indexName + "|" + sourceKey + "|" + source.Id, _ => new SemaphoreSlim(1, 1));
        await documentLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(source.Id)) throw new ArgumentException("Document id is required.", nameof(source));
            var content = Parse(source);
            if (content.Length > ingestionOptions.MaximumDocumentCharacters) throw new InvalidOperationException("Document exceeds the configured maximum size.");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
            var key = indexName + "|" + sourceKey + "|" + source.Id;
            lock (state.Gate) if (state.Entries.TryGetValue(key, out var existing) && existing.Hash == hash) return (0, 0, 1, 0);
            if (string.IsNullOrWhiteSpace(content)) { lock (state.Gate) state.Entries[key] = new RagIngestionState.Entry(hash, []); return (1, 0, 0, 0); }
            var metadata = new Dictionary<string, string>(source.Metadata.Values) { ["documentId"] = source.Id, ["contentHash"] = hash };
            if (!string.IsNullOrEmpty(source.Title)) metadata["title"] = source.Title;
            if (!string.IsNullOrEmpty(source.Source)) metadata["source"] = source.Source;
            if (!string.IsNullOrEmpty(source.Version)) metadata["documentVersion"] = source.Version;
            var document = new RagDocument { Id = source.Id, Content = content, Metadata = new RagDocumentMetadata { SourceId = source.Id, SourceName = source.Title, SourceUri = source.Source, ContentType = source.ContentType, AdditionalMetadata = new RagMetadata(metadata) } };
            var result = await effectiveDocumentIngestion.IngestAsync(document, token).ConfigureAwait(false);
            if (result.Items.Count == 0) return (0, 0, 1, 0);
            var dimensions = result.Items[0].EmbeddingResult.Embedding.Dimensions;
            var index = await effectiveVectorStore.CreateIndexAsync(new CreateVectorIndexRequest { IndexName = indexName, Dimensions = dimensions }, token).ConfigureAwait(false);
            if (!index.Succeeded) throw new InvalidOperationException(index.Reason);
            var persisted = await effectiveUpsertPipeline.UpsertAsync(result, indexName, document.Metadata, dimensions, token).ConfigureAwait(false);
            if (!persisted.Succeeded) throw new InvalidOperationException(persisted.Reason);
            var newChunkIds = result.Chunks.Select(x => x.Id).ToArray();
            RagIngestionState.Entry? old; lock (state.Gate) state.Entries.TryGetValue(key, out old);
            var staleChunkIds = old?.ChunkIds.Except(newChunkIds, StringComparer.Ordinal).ToList() ?? [];
            if (staleChunkIds.Count > 0)
            {
                var deleted = await effectiveVectorStore.DeleteAsync(new DeleteVectorRequest { IndexName = indexName, VectorIds = staleChunkIds }, token).ConfigureAwait(false);
                if (!deleted.Succeeded) throw new InvalidOperationException(deleted.Reason);
            }
            lock (state.Gate) state.Entries[key] = new RagIngestionState.Entry(hash, newChunkIds);
            return old is null ? (1, 0, 0, result.Chunks.Count) : (0, 1, 0, result.Chunks.Count);
        }
        finally { documentLock.Release(); }
    }

    private string Parse(RagSourceDocument document) => document.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ? JsonDocument.Parse(document.Content).RootElement.TryGetProperty(ingestionOptions.JsonTextField, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : throw new InvalidOperationException($"JSON text field '{ingestionOptions.JsonTextField}' is required.") : document.ContentType is "text/plain" or "text/markdown" or "application/pdf" or "" ? document.Content : throw new NotSupportedException($"Content type '{document.ContentType}' is not supported.");
    private static void Validate(string indexName) { if (string.IsNullOrWhiteSpace(indexName)) throw new ArgumentException("Index name is required.", nameof(indexName)); }
    private async Task<int> PropagateDeletesAsync(string sourceKey, string indexName, IEnumerable<string> present, IRagVectorStore effectiveVectorStore, CancellationToken token) { var ids = new HashSet<string>(present, StringComparer.Ordinal); List<(string Key, RagIngestionState.Entry Entry)> stale; lock (state.Gate) stale = state.Entries.Where(x => x.Key.StartsWith(indexName + "|" + sourceKey + "|", StringComparison.Ordinal) && !ids.Contains(x.Key[(indexName.Length + sourceKey.Length + 2)..])).Select(x => (x.Key, x.Value)).ToList(); foreach (var item in stale) { var result = await effectiveVectorStore.DeleteAsync(new DeleteVectorRequest { IndexName = indexName, VectorIds = item.Entry.ChunkIds.ToList() }, token).ConfigureAwait(false); if (!result.Succeeded) throw new InvalidOperationException(result.Reason); lock (state.Gate) state.Entries.Remove(item.Key); } return stale.Count; }

    private (IRagDocumentIngestionService DocumentIngestion, IRagVectorStoreUpsertPipeline UpsertPipeline, IRagVectorStore VectorStore) ResolveIngestionPipeline(string indexName)
    {
        if (runtimeResolver is null || indexRegistry?.Registrations.Any(index => string.Equals(index.Name, indexName, StringComparison.Ordinal)) != true)
            return (documentIngestion, upsertPipeline, vectorStore);
        var runtime = runtimeResolver.Resolve(indexName);
        var options = new RagOptions { Chunking = runtime.Chunking, Ingestion = ragOptions.Ingestion, EmbeddingModel = $"{runtime.EmbeddingModel.ProviderName}/{runtime.EmbeddingModel.ModelName}", EmbeddingProvider = ragOptions.EmbeddingProvider };
        var snapshot = Microsoft.Extensions.Options.Options.Create(options);
        var chunker = new DefaultRagChunker(snapshot);
        var generator = new DefaultRagChunkEmbeddingGenerator(runtime.EmbeddingClient, inputPreparer!, snapshot);
        var ingestion = new DefaultRagDocumentIngestionService(chunker, generator);
        var upsert = new DefaultRagVectorStoreUpsertPipeline(requestMapper!, dimensionValidator!, runtime.VectorStore, telemetryRecorder);
        return (ingestion, upsert, runtime.VectorStore);
    }

    /// <summary>
    /// Gets assembled RAG context for the specified query.
    /// </summary>
    /// <param name="query">The query used to assemble context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The assembled RAG context.</returns>
    public async Task<RagContext> GetContextAsync(
        RagQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await retriever.RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
        var content = results.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, results.Select(result => result.Chunk.Content));

        return new RagContext
        {
            Query = query,
            Results = results.ToList(),
            Content = content,
        };
    }
}

