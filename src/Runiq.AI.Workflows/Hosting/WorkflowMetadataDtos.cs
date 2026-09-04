using System.ComponentModel.DataAnnotations;

namespace Runiq.AI.Core.Workflows;

/// <summary>
/// Describes a registered workflow for dashboard metadata responses.
/// </summary>
/// <param name="Id">The workflow identifier used in dashboard API routes.</param>
/// <param name="Name">The human-readable workflow name.</param>
/// <param name="StartStepId">The first step identifier, or null when the workflow has no steps.</param>
/// <param name="StepCount">The number of steps registered in the workflow.</param>
/// <param name="Steps">The ordered step metadata entries.</param>
public sealed record WorkflowMetadataDto(
    string Id,
    string Name,
    string? StartStepId,
    int StepCount,
    IReadOnlyList<WorkflowStepMetadataDto> Steps);

/// <summary>
/// Describes a single workflow step for dashboard metadata responses.
/// </summary>
/// <param name="Id">The step identifier inside the workflow definition.</param>
/// <param name="AgentType">The full CLR type name of the agent executed by the step.</param>
/// <param name="AgentName">The short CLR type name displayed by the dashboard.</param>
/// <param name="SuccessStepId">The next step identifier for successful execution, if configured.</param>
/// <param name="FailureBehavior">The configured behavior when this step fails.</param>
/// <param name="FailureStepId">The next step identifier for failed execution, if configured.</param>
public sealed record WorkflowStepMetadataDto(
    string Id,
    string AgentType,
    string AgentName,
    string? SuccessStepId,
    string FailureBehavior,
    string? FailureStepId);

/// <summary>
/// Represents a dashboard request to execute a workflow.
/// </summary>
/// <param name="Input">The user input sent to the first workflow step.</param>
public sealed record WorkflowRunRequestDto([property: StringLength(32_768)] string? Input);

/// <summary>
/// Represents the dashboard response returned after a workflow execution.
/// </summary>
/// <param name="WorkflowId">The workflow identifier that was executed.</param>
/// <param name="Status">The final workflow run status.</param>
/// <param name="FinalOutput">The final output produced by the workflow, when available.</param>
/// <param name="ErrorMessage">The workflow-level error message, when execution failed.</param>
/// <param name="Steps">The per-step execution results.</param>
public sealed record WorkflowRunResponseDto(
    string WorkflowId,
    string Status,
    string? FinalOutput,
    string? ErrorMessage,
    IReadOnlyList<WorkflowStepRunResultDto> Steps);

/// <summary>
/// Describes the execution result for a single workflow step.
/// </summary>
/// <param name="StepId">The step identifier inside the workflow definition.</param>
/// <param name="AgentName">The short CLR type name of the step agent.</param>
/// <param name="AgentType">The full CLR type name of the step agent.</param>
/// <param name="Status">The step run status.</param>
/// <param name="Input">The input passed to this step.</param>
/// <param name="Output">The output returned by this step.</param>
/// <param name="ErrorMessage">The step-level error message, when execution failed.</param>
/// <param name="ToolCalls">The tool calls observed while the step executed.</param>
public sealed record WorkflowStepRunResultDto(
    string StepId,
    string AgentName,
    string AgentType,
    string Status,
    string? Input,
    string? Output,
    string? ErrorMessage,
    IReadOnlyList<WorkflowToolCallRunResultDto> ToolCalls);

/// <summary>
/// Describes a tool call observed during workflow execution.
/// </summary>
/// <param name="ToolCallId">The provider tool call identifier, when available.</param>
/// <param name="ToolName">The logical tool name.</param>
/// <param name="Status">The tool call run status.</param>
/// <param name="ArgumentsJson">The JSON arguments supplied to the tool.</param>
/// <param name="OutputJson">The JSON output returned by the tool.</param>
/// <param name="ErrorCode">The tool error code, when execution failed.</param>
/// <param name="ErrorMessage">The tool error message, when execution failed.</param>
/// <param name="StartedAt">The time the tool call started, when recorded.</param>
/// <param name="CompletedAt">The time the tool call completed, when recorded.</param>
/// <param name="DurationMs">The tool call duration in milliseconds, when recorded.</param>
public sealed record WorkflowToolCallRunResultDto(
    string? ToolCallId,
    string? ToolName,
    string Status,
    string? ArgumentsJson,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long? DurationMs);

