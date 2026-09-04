using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Invokes typed tools registered for an agent at runtime.
/// </summary>
public sealed class AgentToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentToolInvoker> logger;

    /// <summary>
    /// Initializes an invoker that resolves tool dependencies from the active service provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve tool dependencies.</param>
    /// <param name="logger">Logger that receives detailed server-side invocation failures.</param>
    public AgentToolInvoker(IServiceProvider serviceProvider, ILogger<AgentToolInvoker>? logger = null)
    {
        _serviceProvider = serviceProvider;
        this.logger = logger ?? NullLogger<AgentToolInvoker>.Instance;
    }

    /// <summary>
    /// Invokes a tool registered on an agent with JSON arguments.
    /// </summary>
    /// <param name="agent">Agent that owns the tool registration.</param>
    /// <param name="toolName">Name of the tool requested by the model.</param>
    /// <param name="argumentsJson">JSON input produced by the model.</param>
    /// <param name="cancellationToken">Token that cancels tool execution.</param>
    /// <returns>The normalized invocation result and JSON output.</returns>
    public async Task<AgentToolInvocationResult> InvokeAsync(
        Agent agent,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return AgentToolInvocationResult.Failure(
                "ToolNameRequired",
                "Tool name cannot be empty.");
        }

        var tool = agent.Tools.FirstOrDefault(candidate =>
            candidate.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return AgentToolInvocationResult.Failure(
                "ToolNotFound",
                $"Agent '{agent.Id}' does not have a tool named '{toolName}'.");
        }

        return await InvokeAsync(
            tool,
            argumentsJson,
            cancellationToken);
    }

    /// <summary>
    /// Invokes a typed tool registration directly with JSON arguments.
    /// </summary>
    /// <param name="tool">Tool registration to invoke.</param>
    /// <param name="argumentsJson">JSON tool input.</param>
    /// <param name="cancellationToken">Token that cancels tool execution.</param>
    /// <returns>The normalized invocation result and JSON output.</returns>
    public async Task<AgentToolInvocationResult> InvokeAsync(
        AgentToolRegistration tool,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        try
        {
            var outputJson = await InvokeCoreAsync(
                tool,
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson,
                cancellationToken);

            return AgentToolInvocationResult.Success(outputJson);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Tool {ToolName} input binding failed.", tool.Name);
            return AgentToolInvocationResult.Failure(
                "ToolInputInvalid",
                $"Tool '{tool.Name}' input has an invalid format.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            logger.LogError(exception.InnerException, "Tool {ToolName} execution failed.", tool.Name);
            return AgentToolInvocationResult.Failure(
                "ToolExecutionFailed",
                "The tool could not be executed.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Tool {ToolName} execution failed.", tool.Name);
            return AgentToolInvocationResult.Failure(
                "ToolExecutionFailed",
                "The tool could not be executed.");
        }
    }

    private async Task<string> InvokeCoreAsync(
        AgentToolRegistration tool,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Deserialize(
            argumentsJson,
            tool.InputType,
            JsonOptions);

        if (input is null)
        {
            throw new JsonException(
                $"Tool '{tool.Name}' input JSON produced a null value.");
        }

        var toolInstance = ActivatorUtilities.CreateInstance(
            _serviceProvider,
            tool.ToolType);

        var executeMethod = tool.ToolType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method =>
                method.Name == nameof(IRuniqTool<object, object>.ExecuteAsync) &&
                method.GetParameters().Length == 2);

        var taskObject = executeMethod.Invoke(
            toolInstance,
            [input, cancellationToken]);

        if (taskObject is not Task task)
        {
            throw new InvalidOperationException(
                $"Tool '{tool.Name}' ExecuteAsync method did not return a Task.");
        }

        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");

        if (resultProperty is null)
        {
            return "{}";
        }

        var output = resultProperty.GetValue(task);

        return JsonSerializer.Serialize(
            output,
            tool.OutputType,
            JsonOptions);
    }
}

/// <summary>
/// Represents the normalized result of a runtime tool invocation.
/// </summary>
/// <param name="IsSuccess">Whether invocation completed successfully.</param>
/// <param name="OutputJson">Serialized output for a successful invocation.</param>
/// <param name="ErrorCode">Stable provider-independent failure code.</param>
/// <param name="ErrorMessage">Safe client-facing failure message.</param>
public sealed record AgentToolInvocationResult(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful tool invocation result.
    /// </summary>
    /// <param name="outputJson">Serialized tool output.</param>
    /// <returns>A successful invocation result.</returns>
    public static AgentToolInvocationResult Success(string outputJson)
    {
        return new AgentToolInvocationResult(
            IsSuccess: true,
            OutputJson: outputJson,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    /// Creates a failed tool invocation result.
    /// </summary>
    /// <param name="errorCode">Stable failure code.</param>
    /// <param name="errorMessage">Safe client-facing failure message.</param>
    /// <returns>A failed invocation result.</returns>
    public static AgentToolInvocationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new AgentToolInvocationResult(
            IsSuccess: false,
            OutputJson: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}
