#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Workflows.Domain;

namespace Runiq.AI.Workflows.Validations;

/// <summary>
/// Validates structural rules of a flow definition.
/// </summary>
public static class FlowDefinitionValidator
{
    public static FlowValidationResult Validate(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var errors = new List<string>();

        if (flow.Steps.Count == 0)
        {
            errors.Add("Flow must contain at least one step.");
            return FlowValidationResult.Failure(errors);
        }

        var duplicateStepIds = flow.Steps
            .GroupBy(step => step.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var duplicateStepId in duplicateStepIds)
        {
            errors.Add($"Flow contains duplicate step id '{duplicateStepId}'.");
        }

        var stepIds = flow.Steps
            .Select(step => step.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var step in flow.Steps)
        {
            if (!string.IsNullOrWhiteSpace(step.SuccessStepId) && !stepIds.Contains(step.SuccessStepId))
            {
                errors.Add($"Step '{step.Id}' has unknown success target '{step.SuccessStepId}'.");
            }

            if (!string.IsNullOrWhiteSpace(step.FailureStepId) && !stepIds.Contains(step.FailureStepId))
            {
                errors.Add($"Step '{step.Id}' has unknown failure target '{step.FailureStepId}'.");
            }
        }

        return errors.Count == 0
            ? FlowValidationResult.Success()
            : FlowValidationResult.Failure(errors);
    }
}

