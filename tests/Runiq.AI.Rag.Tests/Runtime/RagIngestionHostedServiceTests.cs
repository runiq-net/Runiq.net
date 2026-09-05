using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Rag.Tests.Runtime;

public sealed class RagIngestionHostedServiceTests
{
    // Verifies BackgroundOnStartup returns from host startup before ingestion completes and later becomes ready.
    [Fact]
    public async Task BackgroundOnStartup_ShouldNotBlockStartup_AndShouldBecomeReady()
    {
        var service = new SequencedRagService(block: true);
        await using var provider = CreateProvider(service);
        var hosted = Assert.Single(provider.GetServices<IHostedService>());

        await hosted.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var running = provider.GetRequiredService<IRagIngestionManager>().GetStatus("documents");
        Assert.Equal(RagIndexReadiness.Initializing, running.Readiness);
        Assert.Equal(RagIngestionOperationReason.BackgroundStartup, running.ActiveOperation?.Reason);
        service.Release.TrySetResult();
        await service.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        await WaitForReadinessAsync(manager, RagIndexReadiness.Ready);
        Assert.Equal(RagIndexReadiness.Ready, manager.GetStatus("documents").Readiness);
        await hosted.StopAsync(CancellationToken.None);
    }

    // Verifies a first background failure is contained by the hosted service and leaves failed readiness.
    [Fact]
    public async Task BackgroundOnStartup_ShouldContainFailure_AndReportFailedReadiness()
    {
        var service = new SequencedRagService(failInvocations: [1]);
        await using var provider = CreateProvider(service);
        var hosted = Assert.Single(provider.GetServices<IHostedService>());

        await hosted.StartAsync(CancellationToken.None);
        await service.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var status = provider.GetRequiredService<IRagIngestionManager>().GetStatus("documents");
        Assert.Equal(RagIndexReadiness.Failed, status.Readiness);
        Assert.Equal(RagIngestionOperationState.Failed, status.LastOperation?.State);
        await hosted.StopAsync(CancellationToken.None);
    }

    // Verifies a background failure preserves a previously usable index as degraded.
    [Fact]
    public async Task BackgroundOnStartup_ShouldReportDegraded_WhenUsableIndexAlreadyExists()
    {
        var service = new SequencedRagService(failInvocations: [2]);
        await using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        Assert.Equal(RagIngestionOperationState.Completed, (await manager.StartAsync("documents")).State);
        var hosted = Assert.Single(provider.GetServices<IHostedService>());

        await hosted.StartAsync(CancellationToken.None);
        await service.SecondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(RagIndexReadiness.Degraded, manager.GetStatus("documents").Readiness);
        await hosted.StopAsync(CancellationToken.None);
    }

    // Verifies host shutdown cancellation reaches background ingestion and produces a cancelled terminal operation.
    [Fact]
    public async Task BackgroundOnStartup_ShouldPropagateHostShutdownCancellation()
    {
        var service = new SequencedRagService(block: true);
        await using var provider = CreateProvider(service);
        var hosted = Assert.Single(provider.GetServices<IHostedService>());
        await hosted.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await hosted.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        await service.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RagIngestionOperationState.Cancelled, provider.GetRequiredService<IRagIngestionManager>().GetStatus("documents").LastOperation?.State);
    }

    private static ServiceProvider CreateProvider(SequencedRagService service)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
            .AddSource(new TestSource())
            .UseVectorStore("store")
            .UseEmbeddingModel("model")
            .ConfigureIngestion(ingestion => ingestion.BackgroundOnStartup())));
        services.Replace(ServiceDescriptor.Scoped<IRagService>(_ => service));
        return services.BuildServiceProvider();
    }

    private static async Task WaitForReadinessAsync(
        IRagIngestionManager manager,
        RagIndexReadiness expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (manager.GetStatus("documents").Readiness != expected)
            await Task.Delay(10, timeout.Token);
    }

    private sealed class TestSource : IRagDocumentSource
    {
        public string Identity => "safe-source";
        public Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RagSourceDocument>>([]);
    }

    private sealed class SequencedRagService(bool block = false, int[]? failInvocations = null) : IRagService
    {
        private int invocation;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref invocation);
            Started.TrySetResult();
            try
            {
                if (block) await Release.Task.WaitAsync(cancellationToken);
                if (failInvocations?.Contains(current) == true) throw new InvalidOperationException("sensitive provider diagnostic");
                return new RagIngestionReport { DiscoveredDocuments = 1, CreatedDocuments = 1, CreatedChunks = 1 };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
            finally
            {
                Completed.TrySetResult();
                if (current == 2) SecondCompleted.TrySetResult();
            }
        }

        public Task<RagContext> GetContextAsync(RagQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
