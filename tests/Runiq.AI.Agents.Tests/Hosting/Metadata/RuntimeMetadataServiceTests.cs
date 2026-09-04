using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Tests.Hosting.Metadata;

public sealed class RuntimeMetadataServiceTests
{
    // Proves agent metadata exposes the effective reranking settings consumed by Running Behavior.
    [Fact]
    public void GetAgents_RerankingConfigured_ProjectsRunningBehaviorMetadata()
    {
        var agent = new Agent("agent", "Agent", "instructions", "openai/model", "key")
            .UseRag(options =>
            {
                options.IndexName = "documents";
                options.Reranking.Enabled = true;
                options.Reranking.MaximumCandidates = 8;
                options.Reranking.Timeout = TimeSpan.FromSeconds(3.5);
                options.Reranking.FailurePolicy = RagRerankerFailurePolicy.Fail;
            });
        var service = new RuntimeMetadataService([agent]);

        var reranking = Assert.Single(service.GetAgents()).Rag.Reranking;

        Assert.True(reranking.Enabled);
        Assert.Equal(8, reranking.MaximumCandidates);
        Assert.Equal(TimeSpan.FromSeconds(3.5), reranking.Timeout);
        Assert.Equal("Fail", reranking.FailurePolicy);
    }
}
