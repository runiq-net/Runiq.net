using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.Agents;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.CorporateDocumentAssistant;
using Runiq.AI.Rag.CorporateDocumentAssistant.Evaluation;
using Runiq.AI.Rag.CorporateDocumentAssistant.Services;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Rag.Tests.Samples;

public sealed class CorporateRagEvaluationTests
{
    [Fact]
    // Verifies the versioned corporate corpus covers every required product-validation category.
    public void EvaluationSet_ShouldCoverRequiredCorporateScenarios()
    {
        var evaluationSet = LoadEvaluationSet();

        Assert.Equal(1, evaluationSet.SchemaVersion);
        Assert.Contains(evaluationSet.Cases, item => item.Category == "answerable" && item.ExpectedAnswerable);
        Assert.Contains(evaluationSet.Cases, item => item.Category == "not-answerable" && !item.ExpectedAnswerable);
        Assert.Contains(evaluationSet.Cases, item => item.Category == "similar-wrong-document" && item.DistractorDocuments.Count > 0);
        Assert.Contains(evaluationSet.Cases, item => item.Category == "technical-identifier");
        Assert.Contains(evaluationSet.Cases, item => item.Category == "tie" && item.ExpectedTieDocuments?.Count > 1);
    }

    [Fact]
    // Verifies the evaluation calculator reports ranking, answerability, prevention, and reranking-latency metrics from one observation set.
    public void EvaluationReport_ShouldCalculateAllRequiredMetrics()
    {
        var evaluationSet = LoadEvaluationSet();
        var observations = evaluationSet.Cases.Select(item => new CorporateRagEvaluationObservation(
            item.Id,
            item.RelevantDocuments,
            item.ExpectedAnswerable,
            ModelInvoked: item.ExpectedAnswerable,
            RerankingDuration: TimeSpan.FromMilliseconds(12))).ToArray();

        var report = CorporateRagEvaluation.Calculate(evaluationSet, observations);

        Assert.Equal(1, report.ContextPrecision);
        Assert.Equal(1, report.RecallAtK);
        Assert.Equal(5d / 6d, report.MeanReciprocalRank, 6);
        Assert.Equal(1, report.NormalizedDiscountedCumulativeGain);
        Assert.Equal(1, report.AnswerabilityPrecision);
        Assert.Equal(1, report.AnswerabilityRecall);
        Assert.Equal(1, report.WrongAnswerPreventionRate);
        Assert.Equal(TimeSpan.FromMilliseconds(12), report.AverageRerankingLatency);
    }

    [Fact]
    // Verifies the sample's real ingestion-to-dashboard path answers a grounded question and blocks a reranker-classified not-answerable question before model invocation.
    public async Task AgentChatSmoke_ShouldCoverAnswerableAndNotAnswerableEndToEnd()
    {
        var documentsPath = Path.Combine(AppContext.BaseDirectory, "SampleDocuments");
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "test-key" }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        CorporateDocumentAssistantSetup.Configure(services, configuration, documentsPath);
        services.AddSingleton<EvaluationEmbeddingClient>();
        services.AddRagEmbeddingClient("openai/text-embedding-3-small", provider => provider.GetRequiredService<EvaluationEmbeddingClient>());
        await using var provider = services.BuildServiceProvider();
        foreach (var service in provider.GetServices<IHostedService>()) await service.StartAsync(CancellationToken.None);
        using var scope = provider.CreateScope();
        var chat = new RecordingChatClient("Employees receive 20 days of annual leave [1].");
        var agent = Assert.Single(provider.GetServices<Agent>());
        var runtime = new AgentExecutionRuntime(
            [agent], chat, chat, provider.GetRequiredService<AgentToolInvoker>(),
            scope.ServiceProvider.GetRequiredService<IRagRetriever>(), new KeywordOverlapReranker());
        var handler = new AgentChatApiHandler(runtime);

        var answerable = await ExecuteAsync("How many annual leave days do employees receive?");
        var notAnswerable = await ExecuteAsync("Parental entitlement length");

        Assert.True(answerable.IsSuccess);
        Assert.Contains("20 days", answerable.Message, StringComparison.Ordinal);
        Assert.Single(answerable.Citations!);
        var answerableEvidence = Assert.Single(answerable.GroundingEvidence!);
        Assert.Equal(RagAnswerability.Answerable, answerableEvidence.Reranking?.Answerability);
        Assert.True(answerableEvidence.Reranking is { Ran: true, CandidateCount: > 0 });
        Assert.NotEmpty(answerableEvidence.SelectedResults!);
        Assert.True(answerable.Rag?.ContextBudget is not null);

        Assert.True(notAnswerable.IsSuccess);
        Assert.Equal("No relevant information was found in the configured documents.", notAnswerable.Message);
        Assert.True(notAnswerable.Citations is null or { Count: 0 });
        var blockedEvidence = Assert.Single(notAnswerable.GroundingEvidence!);
        Assert.Equal("NotAnswerable", blockedEvidence.NoContextReason.ToString());
        Assert.Equal(RagAnswerability.NotAnswerable, blockedEvidence.Reranking?.Answerability);
        Assert.True(blockedEvidence.Reranking is { Ran: true, CandidateCount: > 0 });
        Assert.Single(chat.Requests);

        async Task<AgentChatResponse> ExecuteAsync(string message)
        {
            var result = await handler.ChatAsync(agent.Id, new AgentChatRequest(message, AgentChatResponseMode.Result),
                new DefaultHttpContext { RequestServices = scope.ServiceProvider }, CancellationToken.None);
            return Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }
    }

    private static CorporateRagEvaluationSet LoadEvaluationSet() => CorporateRagEvaluation.Load(
        Path.Combine(AppContext.BaseDirectory, "Evaluation", "corporate-rag-evaluation.json"));

    private sealed class EvaluationEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse(request.Inputs.Select((text, index) =>
                new EmbeddingResult(index, Vector(text), 3)).ToArray()));

        private static IReadOnlyList<float> Vector(string text)
        {
            var value = text.ToLowerInvariant();
            return
            [
                value.Contains("annual") || value.Contains("parental") || value.Contains("leave") ? 1 : 0,
                value.Contains("security") || value.Contains("incident") ? 1 : 0,
                value.Contains("cs1503") || value.Contains("iragretriever") ? 1 : 0,
            ];
        }
    }

    private sealed class RecordingChatClient(string answer) : IChatClient
    {
        public List<ChatRequest> Requests { get; } = [];

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer), ChatFinishReason.Stop));
        }

        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
            ChatRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ContentDelta, answer);
            yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.Completed, FinishReason: ChatFinishReason.Stop);
            await Task.CompletedTask;
        }
    }
}
