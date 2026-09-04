#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Agents;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Interfaces;

namespace Runiq.AI.Workflows.Services;

/// <summary>
/// Resolves flow steps from the registered agent collection.
/// </summary>
public sealed class RegisteredAgentStepResolver : IAgentStepResolver
{
    private readonly IReadOnlyDictionary<Type, Agent> agentsByType;

    public RegisteredAgentStepResolver(IEnumerable<Agent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        agentsByType = agents.ToDictionary(
            agent => agent.GetType(),
            agent => agent);
    }

    public Agent Resolve(Type agentType)
    {
        ArgumentNullException.ThrowIfNull(agentType);

        if (agentsByType.TryGetValue(agentType, out var agent))
        {
            return agent;
        }

        throw new InvalidOperationException(
            $"No registered flow agent found for type '{agentType.FullName}'.");
    }
}

