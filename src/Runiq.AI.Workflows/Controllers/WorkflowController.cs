using Microsoft.AspNetCore.Mvc;
using Runiq.AI.Core.Workflows;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Models;

namespace Runiq.AI.Workflows.Controllers;

/// <summary>
/// Exposes dashboard operations for listing, inspecting, and executing registered workflows.
/// </summary>
[ApiController]
[Route("api/workflows")]
public sealed class WorkflowController : ControllerBase
{
    private readonly FlowCatalog catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowController"/> class.
    /// </summary>
    /// <param name="catalog">The catalog containing registered workflows.</param>
    public WorkflowController(FlowCatalog catalog)
    {
        this.catalog = catalog;
    }

    /// <summary>
    /// Lists metadata for all registered workflows.
    /// </summary>
    /// <returns>The registered workflow metadata.</returns>
    [HttpGet]
    public IActionResult List() => Ok(catalog.Flows.Select(MapWorkflow).ToList());

    /// <summary>
    /// Gets metadata for one registered workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <returns>The workflow metadata, or a not-found response.</returns>
    [HttpGet("{workflowId}")]
    public IActionResult Get(string workflowId)
    {
        var workflow = catalog.FindById(workflowId);
        return workflow is null ? NotFound() : Ok(MapWorkflow(workflow));
    }

    /// <summary>
    /// Executes a registered workflow with the supplied input.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="request">The workflow input request.</param>
    /// <param name="runtime">The runtime used to execute the workflow.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The workflow result, or an HTTP validation or not-found response.</returns>
    [HttpPost("{workflowId}/run")]
    public async Task<IActionResult> Run(
        string workflowId,
        [FromBody] WorkflowRunRequestDto request,
        [FromServices] IFlowRunner runtime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new { error = "Workflow input cannot be empty." });
        }

        var workflow = catalog.FindById(workflowId);
        if (workflow is null)
        {
            return NotFound();
        }

        var result = await runtime.ExecuteAsync(workflow, request.Input.Trim(), cancellationToken);
        return Ok(new WorkflowRunResponseDto(
            WorkflowId: workflow.Id,
            Status: result.Status.ToString(),
            FinalOutput: result.FinalOutput,
            ErrorMessage: result.ErrorMessage,
            Steps: result.StepResults.Select(step => new WorkflowStepRunResultDto(
                StepId: step.StepId,
                AgentName: step.AgentType.Name,
                AgentType: step.AgentType.FullName ?? step.AgentType.Name,
                Status: step.Status.ToString(),
                Input: step.Input,
                Output: step.Output,
                ErrorMessage: step.ErrorMessage,
                ToolCalls: step.ToolCalls.Select(toolCall => new WorkflowToolCallRunResultDto(
                    ToolCallId: toolCall.ToolCallId,
                    ToolName: toolCall.ToolName,
                    Status: toolCall.Status.ToString(),
                    ArgumentsJson: toolCall.ArgumentsJson,
                    OutputJson: toolCall.OutputJson,
                    ErrorCode: toolCall.ErrorCode,
                    ErrorMessage: toolCall.ErrorMessage,
                    StartedAt: toolCall.StartedAt,
                    CompletedAt: toolCall.CompletedAt,
                    DurationMs: toolCall.DurationMs)).ToList())).ToList()));
    }

    private static WorkflowMetadataDto MapWorkflow(Flow workflow) =>
        new(
            Id: workflow.Id,
            Name: workflow.Name,
            StartStepId: workflow.Steps.Count > 0 ? workflow.Steps[0].Id : null,
            StepCount: workflow.Steps.Count,
            Steps: workflow.Steps.Select(step => new WorkflowStepMetadataDto(
                Id: step.Id,
                AgentType: step.ExecutableType.FullName ?? step.ExecutableType.Name,
                AgentName: step.ExecutableType.Name,
                SuccessStepId: step.SuccessStepId,
                FailureBehavior: step.FailureBehavior.ToString(),
                FailureStepId: step.FailureStepId)).ToList());
}
