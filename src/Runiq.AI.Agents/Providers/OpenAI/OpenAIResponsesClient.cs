using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Providers;

namespace Runiq.AI.Agents.Providers.OpenAI;

/// <summary>
/// Implements provider-neutral chat operations using the native OpenAI Responses protocol.
/// </summary>
public sealed class OpenAIResponsesClient : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a native OpenAI Responses protocol client.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for provider requests.</param>
    public OpenAIResponsesClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var httpRequest = CreateRequest(request, stream: false);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderException(request, response.StatusCode, body);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var toolCalls = ReadToolCalls(root);
            var content = ReadOutputText(root) ?? string.Empty;

            if (toolCalls.Count == 0 && string.IsNullOrWhiteSpace(content))
            {
                throw new ChatProviderException(
                    ProviderErrorKind.MalformedProviderResponse,
                    "Provider response did not contain assistant content or tool calls.",
                    request.Model.ProviderName);
            }

            return new ChatResponse(
                new ChatMessage(ChatRole.Assistant, content, ToolCalls: toolCalls.Count == 0 ? null : toolCalls),
                toolCalls.Count == 0 ? ChatFinishReason.Stop : ChatFinishReason.ToolCalls,
                ReadUsage(root),
                ReadString(root, "id"));
        }
        catch (JsonException exception)
        {
            throw new ChatProviderException(
                ProviderErrorKind.MalformedProviderResponse,
                "Provider returned malformed JSON.",
                request.Model.ProviderName,
                innerException: exception);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        using var httpRequest = CreateRequest(request, stream: true);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateProviderException(request, response.StatusCode, errorBody);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        string? responseId = null;
        ChatUsage? usage = null;
        var finishReason = ChatFinishReason.Stop;
        var pendingToolCalls = new Dictionary<string, PendingToolCall>(StringComparer.Ordinal);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(data);
            }
            catch (JsonException exception)
            {
                throw new ChatProviderException(
                    ProviderErrorKind.MalformedProviderResponse,
                    "Provider returned a malformed stream event.",
                    request.Model.ProviderName,
                    innerException: exception);
            }

            using (document)
            {
                var root = document.RootElement;
                var type = ReadString(root, "type");
                responseId ??= ReadResponseId(root);

                if (type == "response.output_text.delta" && ReadString(root, "delta") is { Length: > 0 } delta)
                {
                    yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ContentDelta, ContentDelta: delta, ProviderResponseId: responseId);
                }
                else if (type == "response.output_item.added" && root.TryGetProperty("item", out var addedItem) &&
                         ReadString(addedItem, "type") == "function_call")
                {
                    var key = ReadString(addedItem, "id") ?? ReadString(addedItem, "call_id") ?? string.Empty;
                    pendingToolCalls[key] = new PendingToolCall(
                        ReadString(addedItem, "call_id") ?? key,
                        ReadString(addedItem, "name") ?? string.Empty);
                }
                else if (type == "response.function_call_arguments.delta")
                {
                    var key = ReadString(root, "item_id") ?? ReadString(root, "call_id") ?? string.Empty;
                    if (!pendingToolCalls.TryGetValue(key, out var pending))
                        pendingToolCalls[key] = pending = new PendingToolCall(ReadString(root, "call_id") ?? key, ReadString(root, "name") ?? string.Empty);
                    pending.Arguments += ReadString(root, "delta") ?? string.Empty;
                }
                else if (type == "response.output_item.done" && TryReadToolCall(root, out var toolCall))
                {
                    var item = root.GetProperty("item");
                    var key = ReadString(item, "id") ?? toolCall.Id;
                    if (pendingToolCalls.TryGetValue(key, out var pending) && string.IsNullOrEmpty(ReadString(item, "arguments")))
                        toolCall = toolCall with { ArgumentsJson = pending.Arguments };
                    finishReason = ChatFinishReason.ToolCalls;
                    yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: toolCall, ProviderResponseId: responseId);
                }
                else if (type == "response.completed" && root.TryGetProperty("response", out var completed))
                {
                    usage = ReadUsage(completed);
                    if (usage is not null)
                    {
                        yield return new ChatStreamingUpdate(ChatStreamingUpdateKind.Usage, Usage: usage, ProviderResponseId: responseId);
                    }
                }
                else if (type is "response.failed" or "error")
                {
                    throw new ChatProviderException(
                        ProviderErrorKind.ProviderUnavailable,
                        ReadProviderError(root),
                        request.Model.ProviderName);
                }
            }
        }

        yield return new ChatStreamingUpdate(
            ChatStreamingUpdateKind.Completed,
            FinishReason: finishReason,
            Usage: usage,
            ProviderResponseId: responseId);
    }

    private static HttpRequestMessage CreateRequest(ChatRequest request, bool stream)
    {
        var endpoint = request.ProviderEndpoint
            ?? ProviderDefaults.ResolveUrl(request.Model.ProviderName, request.Model.ModelName, configuredUrl: null);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri($"{endpoint.ToString().TrimEnd('/')}/responses"));

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }

        var system = string.Join("\n\n", request.Messages.Where(message => message.Role == ChatRole.System).Select(message => message.Content));
        var previousResponseId = request.Options?.Extensions.TryGetValue("previous_response_id", out var value) == true
            ? value as string
            : null;

        httpRequest.Content = JsonContent.Create(
            new ResponseRequest(
                request.Model.ModelName,
                system,
                CreateInput(request.Messages),
                stream,
                CreateReasoningOptions(request),
                CreateTextOptions(request),
                request.Tools?.Select(MapTool).ToArray(),
                previousResponseId),
            options: JsonOptions);
        return httpRequest;
    }

    private static object CreateInput(IReadOnlyList<ChatMessage> messages)
    {
        var nonSystemMessages = messages.Where(message => message.Role != ChatRole.System).ToArray();
        if (nonSystemMessages.Length == 1 && nonSystemMessages[0].Role == ChatRole.User)
        {
            return nonSystemMessages[0].Content;
        }

        return nonSystemMessages.Select(message => message.Role == ChatRole.Tool
            ? (object)new FunctionOutput("function_call_output", message.ToolCallId ?? string.Empty, message.Content)
            : new InputMessage(MapRole(message.Role), message.Content)).ToArray();
    }

    private static TextOptions? CreateTextOptions(ChatRequest request)
    {
        ResponseFormat? format = request.ResponseFormat is null
            ? null
            : new ResponseFormat("json_schema", request.ResponseFormat.Name, ParseSchema(request.ResponseFormat.JsonSchema), true);
        var verbosity = SupportsModelControls(request.Model.ModelName)
            ? request.Options?.Verbosity
            : null;
        return format is null && string.IsNullOrWhiteSpace(verbosity)
            ? null
            : new TextOptions(verbosity, format);
    }

    private static ReasoningOptions? CreateReasoningOptions(ChatRequest request)
    {
        var effort = request.Options?.ReasoningEffort;
        return SupportsModelControls(request.Model.ModelName) && !string.IsNullOrWhiteSpace(effort)
            ? new ReasoningOptions(effort)
            : null;
    }

    private static bool SupportsModelControls(string modelName)
    {
        if (modelName.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return modelName.Length > 1 &&
            (modelName[0] is 'o' or 'O') &&
            char.IsDigit(modelName[1]);
    }

    private static JsonElement ParseSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.Clone();
    }

    private static ToolDefinition MapTool(ChatToolDefinition tool) =>
        new("function", tool.Name, tool.Description, ParseSchema(tool.ParametersJsonSchema));

    private static string MapRole(ChatRole role) => role switch
    {
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => "user"
    };

    private static IReadOnlyList<ChatToolCall> ReadToolCalls(JsonElement root)
    {
        var calls = new List<ChatToolCall>();
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return calls;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (ReadString(item, "type") == "function_call" && TryReadToolCallItem(item, out var call))
            {
                calls.Add(call);
            }
        }
        return calls;
    }

    private static bool TryReadToolCall(JsonElement root, out ChatToolCall call)
    {
        call = default!;
        return root.TryGetProperty("item", out var item) && TryReadToolCallItem(item, out call);
    }

    private static bool TryReadToolCallItem(JsonElement item, out ChatToolCall call)
    {
        var id = ReadString(item, "call_id");
        var name = ReadString(item, "name");
        call = new ChatToolCall(id ?? string.Empty, name ?? string.Empty, ReadString(item, "arguments") ?? "{}");
        return ReadString(item, "type") == "function_call" && !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name);
    }

    private static string? ReadOutputText(JsonElement root)
    {
        if (ReadString(root, "output_text") is { } direct)
        {
            return direct;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Concat(output.EnumerateArray()
            .Where(item => item.TryGetProperty("content", out _))
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(part => ReadString(part, "type") == "output_text")
            .Select(part => ReadString(part, "text") ?? string.Empty));
    }

    private static ChatUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var input = ReadInt(usage, "input_tokens");
        var output = ReadInt(usage, "output_tokens");
        var total = ReadInt(usage, "total_tokens") ?? (input.HasValue && output.HasValue ? input + output : null);
        return new ChatUsage(input, output, total);
    }

    private static string? ReadResponseId(JsonElement root) =>
        root.TryGetProperty("response", out var response) ? ReadString(response, "id") : ReadString(root, "id");

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static string ReadProviderError(JsonElement root)
    {
        if (root.TryGetProperty("error", out var error))
        {
            return ReadString(error, "message") ?? "The provider reported a stream error.";
        }
        return "The provider reported a stream error.";
    }

    private static ChatProviderException CreateProviderException(ChatRequest request, HttpStatusCode statusCode, string body)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ProviderErrorKind.AuthenticationFailure,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ProviderErrorKind.InvalidRequest,
            HttpStatusCode.NotFound => ProviderErrorKind.ModelNotFound,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProviderErrorKind.Timeout,
            HttpStatusCode.TooManyRequests => ProviderErrorKind.RateLimited,
            HttpStatusCode.NotImplemented => ProviderErrorKind.UnsupportedOperation,
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway => ProviderErrorKind.ProviderUnavailable,
            _ => ProviderErrorKind.Unknown
        };
        return new ChatProviderException(kind, ReadErrorMessage(body, statusCode), request.Model.ProviderName, (int)statusCode);
    }

    private static string ReadErrorMessage(string body, HttpStatusCode statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadProviderError(document.RootElement);
        }
        catch (JsonException)
        {
            return $"Provider request failed with status code {(int)statusCode}.";
        }
    }

    private sealed record ResponseRequest(
        string Model,
        string Instructions,
        object Input,
        bool Stream,
        ReasoningOptions? Reasoning,
        TextOptions? Text,
        IReadOnlyList<ToolDefinition>? Tools,
        [property: JsonPropertyName("previous_response_id")] string? PreviousResponseId);
    private sealed record ReasoningOptions(string Effort);
    private sealed record TextOptions(string? Verbosity, ResponseFormat? Format);
    private sealed record ResponseFormat(string Type, string Name, JsonElement Schema, bool Strict);
    private sealed record ToolDefinition(string Type, string Name, string Description, JsonElement Parameters);
    private sealed record FunctionOutput(string Type, [property: JsonPropertyName("call_id")] string CallId, string Output);
    private sealed class PendingToolCall(string id, string name)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Arguments { get; set; } = string.Empty;
    }
    private sealed record InputMessage(string Role, string Content);
}
