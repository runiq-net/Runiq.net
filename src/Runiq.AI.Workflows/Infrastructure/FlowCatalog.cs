#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Workflows.Domain;

namespace Runiq.AI.Workflows.Infrastructure;

/// <summary>
/// Stores the flow definitions registered for the application.
/// </summary>
public sealed class FlowCatalog
{
    private readonly List<Flow> flows = [];

    public IReadOnlyList<Flow> Flows => flows;

    public FlowCatalog AddFlow(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (flows.Any(existing =>
                string.Equals(existing.Id, flow.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Flow with id '{flow.Id}' is already registered.");
        }

        flows.Add(flow);

        return this;
    }

    public Flow? FindById(string flowId)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return null;
        }

        return flows.FirstOrDefault(flow =>
            string.Equals(flow.Id, flowId, StringComparison.OrdinalIgnoreCase));
    }
}

