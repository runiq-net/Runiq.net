#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Validations;
using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Domain;

namespace Runiq.AI.Workflows.Services;

/// <summary>
/// Default use case for validating and running a flow definition.
/// </summary>
public sealed class FlowRunner : IFlowRunner
{
    private readonly IAgentStepResolver agentResolver;
    private readonly IAgentStepExecutor agentExecutor;

    public FlowRunner(
        IAgentStepResolver agentResolver,
        IAgentStepExecutor agentExecutor)
    {
        this.agentResolver = agentResolver ?? throw new ArgumentNullException(nameof(agentResolver));
        this.agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
    }

    public async Task<RunResult> ExecuteAsync(
        Flow flow,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var validationResult = FlowDefinitionValidator.Validate(flow);

        if (!validationResult.IsValid)
        {
            return new RunResult(
                status: RunStatus.Failed,
                stepResults: [],
                errorMessage: string.Join(Environment.NewLine, validationResult.Errors));
        }

        var currentStep = flow.Steps[0];
        var stepResults = new List<StepRunResult>();

        while (currentStep is not null)
        {
            var step = currentStep;
            var stepInput = input;
            var agent = agentResolver.Resolve(step.ExecutableType);

            try
            {
                var agentResult = await agentExecutor.ExecuteAsync(
                    agent,
                    input,
                    cancellationToken);

                if (!agentResult.IsSuccess)
                {
                    stepResults.Add(
                        new StepRunResult(
                            stepId: step.Id,
                            agentType: agent.GetType(),
                            status: StepRunStatus.Failed,
                            input: stepInput,
                            errorMessage: agentResult.ErrorMessage,
                            toolCalls: agentResult.ToolCalls));

                    currentStep = ResolveFailureStep(flow, step);

                    if (currentStep is not null)
                    {
                        continue;
                    }

                    return new RunResult(
                        status: RunStatus.Failed,
                        stepResults: stepResults,
                        errorMessage: agentResult.ErrorMessage);
                }

                var output = agentResult.Output ?? string.Empty;

                stepResults.Add(
                    new StepRunResult(
                        stepId: step.Id,
                        agentType: agent.GetType(),
                        status: StepRunStatus.Completed,
                        input: stepInput,
                        output: output,
                        toolCalls: agentResult.ToolCalls));

                input = output;

                if (string.IsNullOrWhiteSpace(step.SuccessStepId))
                {
                    break;
                }

                currentStep = flow.Steps.FirstOrDefault(
                    x => string.Equals(
                        x.Id,
                        step.SuccessStepId,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                stepResults.Add(
                    new StepRunResult(
                        stepId: step.Id,
                        agentType: agent.GetType(),
                        status: StepRunStatus.Failed,
                        input: stepInput,
                        errorMessage: ex.Message));

                currentStep = ResolveFailureStep(flow, step);

                if (currentStep is not null)
                {
                    continue;
                }

                return new RunResult(
                    status: RunStatus.Failed,
                    stepResults: stepResults,
                    errorMessage: ex.Message);
            }
        }

        return new RunResult(
            status: RunStatus.Completed,
            stepResults: stepResults,
            finalOutput: input);
    }

    private static FlowStep? ResolveFailureStep(
        Flow flow,
        FlowStep step)
    {
        if (step.FailureBehavior is not FailureBehavior.Continue &&
            step.FailureBehavior is not FailureBehavior.GoTo)
        {
            return null;
        }

        return flow.Steps.FirstOrDefault(
            x => string.Equals(
                x.Id,
                step.FailureStepId,
                StringComparison.OrdinalIgnoreCase));
    }
}

