#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

namespace Runiq.AI.Workflows.Models;

/// <summary>
/// Captures the result returned by a single agent step execution.
/// </summary>
public sealed class AgentStepResult
{
    public AgentStepResult(
        bool isSuccess,
        string? output,
        IReadOnlyList<ToolCallRunResult> toolCalls,
        string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Output = output;
        ToolCalls = toolCalls ?? throw new ArgumentNullException(nameof(toolCalls));
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? Output { get; }

    public IReadOnlyList<ToolCallRunResult> ToolCalls { get; }

    public string? ErrorMessage { get; }

    public static AgentStepResult Success(
        string output,
        IReadOnlyList<ToolCallRunResult> toolCalls)
    {
        return new AgentStepResult(
            isSuccess: true,
            output: output,
            toolCalls: toolCalls);
    }

    public static AgentStepResult Failure(
        string errorMessage,
        IReadOnlyList<ToolCallRunResult> toolCalls)
    {
        return new AgentStepResult(
            isSuccess: false,
            output: null,
            toolCalls: toolCalls,
            errorMessage: errorMessage);
    }
}

