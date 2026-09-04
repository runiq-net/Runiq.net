#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Interfaces;

namespace Runiq.AI.Workflows.Services;

/// <summary>
/// Executes agent-backed flow steps through the Runiq agent runtime.
/// </summary>
public sealed class RuniqAgentStepExecutor : IAgentStepExecutor
{
    private readonly AgentExecutionRuntime agentExecutionRuntime;

    public RuniqAgentStepExecutor(AgentExecutionRuntime agentExecutionRuntime)
    {
        this.agentExecutionRuntime = agentExecutionRuntime
            ?? throw new ArgumentNullException(nameof(agentExecutionRuntime));
    }

    public async Task<AgentStepResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var result = await agentExecutionRuntime.ExecuteAsync(
            agent,
            input,
            cancellationToken);

        var toolCalls = result.Steps
            .Where(step => step.Kind == AgentExecutionStepKind.ToolCall)
            .Select(MapToolCall)
            .ToArray();

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
        {
            return AgentStepResult.Success(result.Message, toolCalls);
        }

        return AgentStepResult.Failure(
            result.ErrorMessage ?? "Agent execution failed.",
            toolCalls);
    }

    private static ToolCallRunResult MapToolCall(AgentExecutionStep step)
    {
        return new ToolCallRunResult(
            toolCallId: step.ToolCallId,
            toolName: step.ToolName,
            status: MapToolCallStatus(step.Status),
            argumentsJson: step.ArgumentsJson,
            outputJson: step.OutputJson,
            errorCode: step.ErrorCode,
            errorMessage: step.ErrorMessage,
            startedAt: step.StartedAt,
            completedAt: step.CompletedAt);
    }

    private static ToolCallRunStatus MapToolCallStatus(
        AgentExecutionStepStatus status)
    {
        return status switch
        {
            AgentExecutionStepStatus.Completed => ToolCallRunStatus.Completed,
            AgentExecutionStepStatus.Failed => ToolCallRunStatus.Failed,
            _ => ToolCallRunStatus.Running
        };
    }
}

