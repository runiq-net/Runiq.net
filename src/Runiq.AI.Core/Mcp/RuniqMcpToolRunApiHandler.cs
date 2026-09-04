using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Runiq.AI.Core.Mcp;

/// <summary>
/// Handles dashboard MCP tool playground requests.
/// </summary>
public sealed class RuniqMcpToolRunApiHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider services;

    /// <summary>
    /// Creates a new MCP tool run API handler.
    /// </summary>
    public RuniqMcpToolRunApiHandler(IServiceProvider services)
    {
        this.services = services;
    }

    /// <summary>
    /// Runs an exposed MCP tool with dashboard-provided input.
    /// </summary>
    public async Task<IResult> RunAsync(
        string toolName,
        RuniqMcpToolRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Results.BadRequest(new RuniqMcpToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNameRequired",
                ErrorMessage: "Tool name is required."));
        }

        var tool = RuniqMcpToolCatalog.FindTool(toolName);

        if (tool is null)
        {
            return Results.NotFound(new RuniqMcpToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNotFound",
                ErrorMessage: $"MCP tool '{toolName}' could not be found."));
        }

        var binding = BindArguments(tool.Method, request?.Input, cancellationToken);
        if (binding.Errors.Count > 0)
        {
            return Results.ValidationProblem(
                binding.Errors,
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more MCP tool inputs are invalid.");
        }

        try
        {
            var instance = tool.Method.IsStatic ? null : CreateToolInstance(tool.ToolType);
            var invocationResult = tool.Method.Invoke(instance, binding.Arguments);
            var output = await UnwrapInvocationResultAsync(invocationResult);

            return Results.Ok(new RuniqMcpToolRunResponse(
                IsSuccess: true,
                OutputJson: JsonSerializer.Serialize(output, SerializerOptions),
                ErrorCode: null,
                ErrorMessage: null));
        }
        catch (TargetInvocationException exception)
        {
            return Results.Ok(CreateFailureResponse(exception.InnerException ?? exception));
        }
        catch (ArgumentException exception)
        {
            return Results.Ok(CreateFailureResponse(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Ok(CreateFailureResponse(exception));
        }
        catch (NotSupportedException exception)
        {
            return Results.Ok(CreateFailureResponse(exception));
        }
    }

    private static ArgumentBindingResult BindArguments(
        MethodInfo method,
        JsonElement? input,
        CancellationToken cancellationToken)
    {
        var inputElement = input is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null }
            ? input.Value
            : default;

        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nullabilityContext = new NullabilityInfoContext();

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments[index] = cancellationToken;
                continue;
            }

            var propertyName = ToJsonPropertyName(parameter.Name ?? string.Empty);
            var propertyValue = default(JsonElement);
            var hasValue = inputElement.ValueKind == JsonValueKind.Object &&
                inputElement.TryGetProperty(propertyName, out propertyValue);
            var isRequired = IsRequired(parameter, nullabilityContext);

            if (!hasValue)
            {
                if (isRequired)
                {
                    errors[propertyName] = ["The field is required."];
                }
                else
                {
                    arguments[index] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
                }

                continue;
            }

            if (propertyValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (isRequired)
                {
                    errors[propertyName] = ["The field cannot be null."];
                }
                else
                {
                    arguments[index] = null;
                }

                continue;
            }

            try
            {
                arguments[index] = DeserializeValue(propertyValue, parameter.ParameterType);
            }
            catch (JsonException)
            {
                errors[propertyName] = ["The value has an invalid format."];
            }
            catch (NotSupportedException)
            {
                errors[propertyName] = ["The value has an unsupported format."];
            }
        }

        return new ArgumentBindingResult(arguments, errors);
    }

    private static bool IsRequired(
        ParameterInfo parameter,
        NullabilityInfoContext nullabilityContext)
    {
        if (parameter.HasDefaultValue || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
        {
            return false;
        }

        if (parameter.ParameterType.IsValueType)
        {
            return true;
        }

        return nullabilityContext.Create(parameter).ReadState == NullabilityState.NotNull;
    }

    private static object? DeserializeValue(JsonElement value, Type targetType)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            value.GetRawText(),
            targetType,
            SerializerOptions);
    }

    private object CreateToolInstance(Type toolType)
    {
        return services.GetService(toolType) ??
            ActivatorUtilities.CreateInstance(services, toolType);
    }

    private static async Task<object?> UnwrapInvocationResultAsync(object? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");

            return resultProperty?.GetValue(task);
        }

        var resultType = result.GetType();

        if (resultType.IsGenericType &&
            resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTaskMethod = resultType.GetMethod("AsTask", Type.EmptyTypes);
            var valueTaskResult = (Task?)asTaskMethod?.Invoke(result, null);

            if (valueTaskResult is null)
            {
                return null;
            }

            await valueTaskResult.ConfigureAwait(false);

            return valueTaskResult.GetType().GetProperty("Result")?.GetValue(valueTaskResult);
        }

        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return null;
        }

        return result;
    }

    private static RuniqMcpToolRunResponse CreateFailureResponse(Exception exception)
    {
        return new RuniqMcpToolRunResponse(
            IsSuccess: false,
            OutputJson: null,
            ErrorCode: exception.GetType().Name,
            ErrorMessage: exception.Message);
    }

    private static string ToJsonPropertyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private sealed record ArgumentBindingResult(
        object?[] Arguments,
        Dictionary<string, string[]> Errors);
}

