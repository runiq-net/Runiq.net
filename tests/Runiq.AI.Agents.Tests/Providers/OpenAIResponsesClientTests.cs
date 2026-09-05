using System.Net;
using System.Text;
using System.Text.Json;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Agents.Tests.Providers;

/// <summary>
/// Verifies the native Responses protocol mapping at the shared chat-client boundary.
/// </summary>
public sealed class OpenAIResponsesClientTests
{
    // Verifies that the provider client exposes only the shared chat-client invocation surface.
    [Fact]
    public void Client_ShouldImplementSharedChatClient()
    {
        Assert.IsAssignableFrom<IChatClient>(new OpenAIResponsesClient(new HttpClient()));
    }

    // Verifies that non-streaming text, usage, finish reason, and response id map to Core contracts.
    [Fact]
    public async Task CompleteAsync_ShouldMapTextAndMetadata()
    {
        var handler = new CapturingHandler("""
            {"id":"resp_1","output_text":"hello","usage":{"input_tokens":2,"output_tokens":3,"total_tokens":5}}
            """, "application/json");
        var client = new OpenAIResponsesClient(new HttpClient(handler));

        var response = await client.CompleteAsync(CreateRequest());

        Assert.Equal("hello", response.Message.Content);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(5, response.Usage?.TotalTokens);
        Assert.Equal("resp_1", response.ProviderResponseId);
    }

    // Verifies that streaming tool calls retain the Responses call_id and remain unexecuted by the provider client.
    [Fact]
    public async Task CompleteStreamingAsync_ShouldMapToolCallWithoutExecutingIt()
    {
        var stream = string.Join("\n",
            "data: {\"type\":\"response.created\",\"response\":{\"id\":\"resp_tools\"}}",
            "data: {\"type\":\"response.output_item.done\",\"item\":{\"type\":\"function_call\",\"call_id\":\"call_123\",\"name\":\"echo\",\"arguments\":\"{}\"}}",
            "data: [DONE]", string.Empty);
        var client = new OpenAIResponsesClient(new HttpClient(new CapturingHandler(stream, "text/event-stream")));

        var updates = await client.CompleteStreamingAsync(CreateRequest()).ToListAsync();

        var toolUpdate = Assert.Single(updates, update => update.Kind == ChatStreamingUpdateKind.ToolCallDelta);
        Assert.Equal("call_123", toolUpdate.ToolCall?.Id);
        Assert.Equal("echo", toolUpdate.ToolCall?.Name);
        Assert.Equal(ChatFinishReason.ToolCalls, updates[^1].FinishReason);
    }

    // Verifies that tool results and the prior response id map to a Responses continuation payload.
    [Fact]
    public async Task CompleteStreamingAsync_ShouldMapToolResultContinuation()
    {
        var handler = new CapturingHandler("data: [DONE]\n", "text/event-stream");
        var client = new OpenAIResponsesClient(new HttpClient(handler));
        var options = new ChatRequestOptions();
        options.Extensions["previous_response_id"] = "resp_previous";
        var request = CreateRequest() with
        {
            Messages = [new ChatMessage(ChatRole.Tool, "{\"ok\":true}", "call_exact")],
            Options = options
        };

        await client.CompleteStreamingAsync(request).ToListAsync();

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        var root = document.RootElement;
        Assert.Equal("resp_previous", root.GetProperty("previous_response_id").GetString());
        Assert.Equal("call_exact", root.GetProperty("input")[0].GetProperty("call_id").GetString());
    }

    // Verifies that Responses argument delta events are combined before the Agent runtime receives a tool call.
    [Fact]
    public async Task CompleteStreamingAsync_ShouldAggregateToolArgumentDeltas()
    {
        var stream = string.Join("\n",
            "data: {\"type\":\"response.output_item.added\",\"item\":{\"id\":\"item_1\",\"type\":\"function_call\",\"call_id\":\"call_1\",\"name\":\"echo\"}}",
            "data: {\"type\":\"response.function_call_arguments.delta\",\"item_id\":\"item_1\",\"delta\":\"{\\\"value\\\":\"}",
            "data: {\"type\":\"response.function_call_arguments.delta\",\"item_id\":\"item_1\",\"delta\":\"1}\"}",
            "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item_1\",\"type\":\"function_call\",\"call_id\":\"call_1\",\"name\":\"echo\",\"arguments\":\"\"}}",
            "data: [DONE]", string.Empty);

        var updates = await new OpenAIResponsesClient(new HttpClient(new CapturingHandler(stream, "text/event-stream")))
            .CompleteStreamingAsync(CreateRequest()).ToListAsync();

        var call = Assert.Single(updates, update => update.Kind == ChatStreamingUpdateKind.ToolCallDelta).ToolCall;
        Assert.Equal("{\"value\":1}", call?.ArgumentsJson);
    }

    // Verifies that structured output and tool definitions are emitted in the native Responses request shape.
    [Fact]
    public async Task CompleteAsync_ShouldMapStructuredOutputAndTools()
    {
        var handler = new CapturingHandler("{\"id\":\"resp_1\",\"output_text\":\"{}\"}", "application/json");
        var request = CreateRequest() with
        {
            ResponseFormat = new ChatResponseFormat("answer", "{\"type\":\"object\"}"),
            Tools = [new ChatToolDefinition("echo", "Echo input", "{\"type\":\"object\"}")]
        };

        await new OpenAIResponsesClient(new HttpClient(handler)).CompleteAsync(request);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal("json_schema", document.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("echo", document.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
    }

    // Verifies that GPT-4.1 requests omit model controls that the non-reasoning model rejects.
    [Fact]
    public async Task CompleteAsync_ShouldOmitModelControls_WhenModelDoesNotSupportThem()
    {
        var handler = new CapturingHandler("{\"id\":\"resp_1\",\"output_text\":\"ok\"}", "application/json");
        var request = CreateRequest() with
        {
            Model = ModelReference.Parse("openai/gpt-4.1-mini"),
            Options = new ChatRequestOptions
            {
                ReasoningEffort = "minimal",
                Verbosity = "low"
            }
        };

        await new OpenAIResponsesClient(new HttpClient(handler)).CompleteAsync(request);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.False(document.RootElement.TryGetProperty("reasoning", out _));
        Assert.False(document.RootElement.TryGetProperty("text", out _));
    }

    // Verifies that GPT-5 requests retain supported reasoning and verbosity controls.
    [Fact]
    public async Task CompleteAsync_ShouldIncludeModelControls_WhenModelSupportsThem()
    {
        var handler = new CapturingHandler("{\"id\":\"resp_1\",\"output_text\":\"ok\"}", "application/json");
        var request = CreateRequest() with
        {
            Options = new ChatRequestOptions
            {
                ReasoningEffort = "minimal",
                Verbosity = "low"
            }
        };

        await new OpenAIResponsesClient(new HttpClient(handler)).CompleteAsync(request);

        using var document = JsonDocument.Parse(handler.RequestBodies.Single());
        Assert.Equal("minimal", document.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("low", document.RootElement.GetProperty("text").GetProperty("verbosity").GetString());
    }

    // Verifies that HTTP status failures use the shared provider error contract.
    [Fact]
    public async Task CompleteAsync_ShouldMapHttpError()
    {
        var client = new OpenAIResponsesClient(new HttpClient(new CapturingHandler(
            "{\"error\":{\"message\":\"missing model\"}}", "application/json", HttpStatusCode.NotFound)));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(() => client.CompleteAsync(CreateRequest()));

        Assert.Equal(ProviderErrorKind.ModelNotFound, exception.Kind);
    }

    // Verifies that cancellation remains observable to Agent orchestration instead of becoming a mapped HTTP error.
    [Fact]
    public async Task CompleteAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new OpenAIResponsesClient(new HttpClient(new CapturingHandler("{}", "application/json")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CompleteAsync(CreateRequest(), cancellation.Token));
    }

    private static ChatRequest CreateRequest() => new(
        ModelReference.Parse("openai/gpt-5"),
        [new ChatMessage(ChatRole.User, "hello")],
        new Uri("https://api.example.test/v1"),
        "test-key");

    private sealed class CapturingHandler(
        string responseBody,
        string mediaType,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
            };
        }
    }
}
