#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Interfaces;

/// <summary>
/// Resolves step executable types to registered agent instances.
/// </summary>
public interface IAgentStepResolver
{
    Agent Resolve(Type agentType);
}

