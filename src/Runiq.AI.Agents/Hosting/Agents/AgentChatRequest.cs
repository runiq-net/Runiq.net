using System.ComponentModel.DataAnnotations;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Studio �zerinden agent'a g�nderilen chat istegini temsil eder.
/// </summary>
public sealed record AgentChatRequest(
    [property: Required]
    [property: StringLength(16_000, MinimumLength = 1)]
    string Message,
    [property: EnumDataType(typeof(AgentChatResponseMode))]
    AgentChatResponseMode ResponseMode = AgentChatResponseMode.Stream,
    [property: StringLength(128)]
    [property: RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    string? IndexName = null);

