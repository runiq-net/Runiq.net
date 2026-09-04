#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Workflows.Validations;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Infrastructure;

namespace Runiq.AI.Workflows;

/// <summary>
/// Configures flow definitions registered through AddRuniqWorkflows.
/// </summary>
public sealed class FlowOptions
{
    private readonly FlowCatalog catalog = new();

    public IReadOnlyList<Flow> Flows => catalog.Flows;

    public FlowOptions AddFlow(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var validationResult = FlowDefinitionValidator.Validate(flow);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Flow '{flow.Id}' is invalid:{Environment.NewLine}" +
                string.Join(Environment.NewLine, validationResult.Errors));
        }

        catalog.AddFlow(flow);

        return this;
    }
}

