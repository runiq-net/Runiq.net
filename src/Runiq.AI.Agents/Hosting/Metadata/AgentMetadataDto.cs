namespace Runiq.AI.Core.Metadata;

/// <summary>
/// Studio tarafina donen agent metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentMetadataDto(
    string Id,
    string Name,
    string Instructions,
    string Model,
    string ReasoningEffort,
    string Verbosity,
    AgentRagMetadataDto Rag,
    IReadOnlyList<AgentToolMetadataDto> Tools);

/// <summary>
/// Describes the framework-owned retrieval configuration shown by the agent inspector.
/// </summary>
public sealed record AgentRagMetadataDto(
    bool Enabled,
    string? IndexName,
    string? ExecutionMode,
    AgentRerankingMetadataDto Reranking);

/// <summary>Describes the configured reranking behavior shown by the agent inspector.</summary>
public sealed record AgentRerankingMetadataDto(
    bool Enabled,
    int MaximumCandidates,
    TimeSpan Timeout,
    string FailurePolicy);

/// <summary>
/// Studio tarafinda gosterilecek agent tool metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentToolMetadataDto(
    string Name,
    string DisplayName,
    string Description,
    string InputType,
    string OutputType);

/// <summary>
/// Studio tarafinda gosterilecek sistem geneli tool metadata bilgisini temsil eder.
/// </summary>
public sealed record ToolMetadataDto(
    string Name,
    string DisplayName,
    string Description,
    string TypeName,
    string InputType,
    string OutputType,
    bool HasInput,
    IReadOnlyDictionary<string, object?> InputSchema,
    IReadOnlyDictionary<string, object?> OutputSchema,
    IReadOnlyList<ToolAttachedAgentMetadataDto> AttachedAgents);

/// <summary>
/// Bir tool'un bagli oldugu agent bilgisini temsil eder.
/// </summary>
public sealed record ToolAttachedAgentMetadataDto(
    string Id,
    string Name);
