#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

namespace Runiq.AI.Workflows.Models;

/// <summary>
/// Captures the final result of a flow run.
/// </summary>
public sealed class RunResult
{
    public RunResult(
        RunStatus status,
        IReadOnlyList<StepRunResult> stepResults,
        string? finalOutput = null,
        string? errorMessage = null)
    {
        Status = status;
        StepResults = stepResults ?? throw new ArgumentNullException(nameof(stepResults));
        FinalOutput = finalOutput;
        ErrorMessage = errorMessage;
    }

    public RunStatus Status { get; }

    public IReadOnlyList<StepRunResult> StepResults { get; }

    public string? FinalOutput { get; }

    public string? ErrorMessage { get; }
}

