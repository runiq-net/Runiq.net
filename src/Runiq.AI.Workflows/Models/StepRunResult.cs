#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

namespace Runiq.AI.Workflows.Models;

/// <summary>
/// Captures the result of a single flow step.
/// </summary>
public sealed class StepRunResult
{
    public StepRunResult(
        string stepId,
        Type agentType,
        StepRunStatus status,
        string? input = null,
        string? output = null,
        string? errorMessage = null,
        IReadOnlyList<ToolCallRunResult>? toolCalls = null)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Flow step id cannot be empty.", nameof(stepId));
        }

        StepId = stepId.Trim();
        AgentType = agentType ?? throw new ArgumentNullException(nameof(agentType));
        Status = status;
        Input = input;
        Output = output;
        ErrorMessage = errorMessage;
        ToolCalls = toolCalls ?? [];
    }

    public string StepId { get; }

    public Type AgentType { get; }

    public StepRunStatus Status { get; }

    public string? Input { get; }

    public string? Output { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyList<ToolCallRunResult> ToolCalls { get; }
}

