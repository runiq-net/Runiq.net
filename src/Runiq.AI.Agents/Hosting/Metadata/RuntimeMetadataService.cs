using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Core.Metadata;

/// <summary>
/// Host uygulamada DI container'a register edilmis agent ve tool kayitlarini metadata DTO modellerine map eder.
/// </summary>
internal sealed class RuntimeMetadataService : IRuntimeMetadataService
{
    private readonly IEnumerable<Agent> _agents;
    private readonly IReadOnlyList<AgentToolRegistration> _registeredTools;

    public RuntimeMetadataService(
        IEnumerable<Agent> agents,
        IReadOnlyList<AgentToolRegistration>? registeredTools = null)
    {
        _agents = agents;
        _registeredTools = registeredTools ?? [];
    }

    public IReadOnlyList<AgentMetadataDto> GetAgents()
    {
        return _agents
            .Select(agent => new AgentMetadataDto(
                Id: agent.Id,
                Name: agent.Name,
                Instructions: agent.Instructions,
                Model: agent.Model,
                ReasoningEffort: agent.ReasoningEffort,
                Verbosity: agent.Verbosity,
                Rag: new AgentRagMetadataDto(
                    Enabled: agent.Rag?.Enabled == true,
                    IndexName: agent.Rag?.IndexName,
                    ExecutionMode: agent.Rag?.Enabled == true ? agent.Rag.Mode.ToString() : null,
                    Reranking: new AgentRerankingMetadataDto(
                        Enabled: agent.Rag?.Reranking.Enabled == true,
                        MaximumCandidates: agent.Rag?.Reranking.MaximumCandidates ?? 5,
                        Timeout: agent.Rag?.Reranking.Timeout ?? TimeSpan.FromSeconds(5),
                        FailurePolicy: (agent.Rag?.Reranking.FailurePolicy ??
                            RagRerankerFailurePolicy.UseOriginalOrder).ToString())),
                Tools: agent.Tools.Select(MapAgentTool).ToList()))
            .ToList();
    }

    public IReadOnlyList<ToolMetadataDto> GetTools()
    {
        var agents = _agents.ToArray();

        return _registeredTools
            .Select(tool => new ToolMetadataDto(
                Name: tool.Name,
                DisplayName: FormatDisplayName(tool.Name),
                Description: tool.Description,
                TypeName: tool.ToolType.Name,
                InputType: tool.InputType.Name,
                OutputType: tool.OutputType.Name,
                HasInput: ToolJsonSchemaGenerator.HasInput(tool.InputType),
                InputSchema: ToolJsonSchemaGenerator.CreateSchema(tool.InputType),
                OutputSchema: ToolJsonSchemaGenerator.CreateSchema(tool.OutputType),
                AttachedAgents: agents
                    .Where(agent => agent.Tools.Any(agentTool =>
                        agentTool.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase) &&
                        agentTool.ToolType == tool.ToolType))
                    .Select(agent => new ToolAttachedAgentMetadataDto(
                        Id: agent.Id,
                        Name: agent.Name))
                    .ToList()))
            .ToList();
    }

    private static AgentToolMetadataDto MapAgentTool(AgentToolRegistration tool)
    {
        return new AgentToolMetadataDto(
            Name: tool.Name,
            DisplayName: FormatDisplayName(tool.Name),
            Description: tool.Description,
            InputType: tool.InputType.Name,
            OutputType: tool.OutputType.Name);
    }

    private static string FormatDisplayName(string value)
    {
        return string.Join(
            " ",
            value
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
