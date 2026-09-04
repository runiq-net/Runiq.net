namespace Runiq.AI.Core.Metadata;

/// <summary>
/// Studio tarafina donen agent metadata bilgisini temsil eder.
/// </summary>
/// <param name="Id">The unique agent identifier.</param>
/// <param name="Name">The display name of the agent.</param>
/// <param name="Instructions">The instructions configured for the agent.</param>
/// <param name="Model">The configured model identifier.</param>
/// <param name="ReasoningEffort">The configured reasoning effort.</param>
/// <param name="Verbosity">The configured response verbosity.</param>
/// <param name="Rag">The retrieval configuration exposed to the dashboard.</param>
/// <param name="Tools">The tools attached to the agent.</param>
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
/// <param name="Enabled">Indicates whether retrieval is enabled.</param>
/// <param name="IndexName">The configured retrieval index name, when available.</param>
/// <param name="ExecutionMode">The configured retrieval execution mode, when available.</param>
/// <param name="Reranking">The configured second-stage reranking behavior.</param>
public sealed record AgentRagMetadataDto(
    bool Enabled,
    string? IndexName,
    string? ExecutionMode,
    AgentRerankingMetadataDto Reranking);

/// <summary>Describes the configured reranking behavior shown by the agent inspector.</summary>
/// <param name="Enabled">Indicates whether reranking is enabled.</param>
/// <param name="MaximumCandidates">The maximum number of candidates passed to the reranker.</param>
/// <param name="Timeout">The maximum allowed reranking duration.</param>
/// <param name="FailurePolicy">The policy applied when reranking fails.</param>
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
