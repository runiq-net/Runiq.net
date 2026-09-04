using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Ingestion;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Services;

namespace Runiq.AI.Rag.Tests.Services;

public sealed class RagServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenRetrieverIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagService(null!));

        Assert.Equal("retriever", exception.ParamName);
    }

    [Fact]
    public async Task GetContextAsync_ShouldCallRetriever()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);
        var query = new RagQuery { Text = "query" };

        await service.GetContextAsync(query);

        Assert.True(retriever.WasCalled);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnOriginalQuery()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);
        var query = new RagQuery { Text = "query" };

        var context = await service.GetContextAsync(query);

        Assert.Same(query, context.Query);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnRetrievedResults()
    {
        var results = new List<RagSearchResult>
        {
            new()
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-1",
                    DocumentId = "document-1",
                    Content = "First chunk",
                },
            },
        };
        var retriever = new TrackingRetriever(results);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Single(context.Results);
        Assert.Same(results[0], context.Results[0]);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnEmptyContent_WhenThereAreNoResults()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Equal(string.Empty, context.Content);
    }

    [Fact]
    public async Task GetContextAsync_ShouldJoinChunkContentWithNewLine_WhenResultsExist()
    {
        var retriever = new TrackingRetriever(
        [
            new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-1",
                    DocumentId = "document-1",
                    Content = "First chunk",
                },
            },
            new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-2",
                    DocumentId = "document-1",
                    Content = "Second chunk",
                },
            },
        ]);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Equal($"First chunk{Environment.NewLine}Second chunk", context.Content);
    }

    // Verifies that updating stable chunk identifiers removes only chunks absent from the new document version.
    [Fact]
    public async Task IngestAsync_WhenDocumentChanges_DeletesOnlyStaleChunkIdentifiers()
    {
        var ingestion = new VersionedIngestionService();
        var store = new TrackingVectorStore();
        var service = new RagService(new TrackingRetriever([]), ingestion, new SuccessfulUpsertPipeline(), store, new RagIngestionState());

        await service.IngestAsync(new RagSourceDocument { Id = "document", Content = "first", ContentType = "text/plain" }, "index");
        await service.IngestAsync(new RagSourceDocument { Id = "document", Content = "second", ContentType = "text/plain" }, "index");

        Assert.Equal(["document:chunk:1"], Assert.Single(store.DeleteRequests).VectorIds);
        Assert.DoesNotContain("document:chunk:0", store.DeleteRequests.SelectMany(request => request.VectorIds));
    }

    private sealed class TrackingRetriever : IRagRetriever
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public TrackingRetriever(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            return Task.FromResult(results);
        }
    }

    private sealed class VersionedIngestionService : IRagDocumentIngestionService
    {
        public Task<RagDocumentIngestionResult> IngestAsync(RagDocument document, CancellationToken cancellationToken = default)
        {
            var count = document.Content == "first" ? 2 : 1;
            var chunks = Enumerable.Range(0, count).Select(index => new RagChunk
            {
                Id = $"{document.Id}:chunk:{index}", DocumentId = document.Id, Content = document.Content, Index = index,
            }).ToArray();
            return Task.FromResult(new RagDocumentIngestionResult
            {
                DocumentId = document.Id,
                Chunks = chunks,
                Items = chunks.Select(chunk => new RagDocumentIngestionItem
                {
                    Chunk = chunk,
                    EmbeddingResult = new RagChunkEmbeddingResult { ChunkId = chunk.Id, DocumentId = document.Id, ChunkIndex = chunk.Index, Embedding = new RagEmbedding([1f]) },
                }).ToArray(),
            });
        }
    }

    private sealed class SuccessfulUpsertPipeline : IRagVectorStoreUpsertPipeline
    {
        public Task<UpsertVectorResult> UpsertAsync(RagDocumentIngestionResult ingestionResult, string indexName, RagDocumentMetadata? documentMetadata = null, int? expectedDimensions = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpsertVectorResult { Succeeded = true, ProcessedCount = ingestionResult.Items.Count });

        public Task<UpsertVectorResult> UpsertAsync(UpsertVectorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpsertVectorResult { Succeeded = true, ProcessedCount = request.Records.Count });
    }

    private sealed class TrackingVectorStore : IRagVectorStore
    {
        public List<DeleteVectorRequest> DeleteRequests { get; } = [];
        public Task<CreateVectorIndexResult> CreateIndexAsync(CreateVectorIndexRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new CreateVectorIndexResult { IndexName = request.IndexName, Succeeded = true });
        public Task<UpsertVectorResult> UpsertAsync(UpsertVectorRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new UpsertVectorResult { Succeeded = true });
        public Task<DeleteVectorResult> DeleteAsync(DeleteVectorRequest request, CancellationToken cancellationToken = default) { DeleteRequests.Add(request); return Task.FromResult(new DeleteVectorResult { Succeeded = true, RequestedCount = request.VectorIds.Count, DeletedCount = request.VectorIds.Count, VectorIds = request.VectorIds }); }
        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(RagQuery query, RagEmbedding embedding, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RagSearchResult>>([]);
    }
}

