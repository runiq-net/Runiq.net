using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Tests.Tools;

public sealed class AgentToolInvokerTests
{
    // Verifies that a registered tool executes with valid JSON input and returns output JSON.
    [Fact]
    public async Task InvokeAsync_ShouldExecuteRegisteredTool_WhenInputJsonIsValid()
    {
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            """{"city":"Istanbul"}""");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("Istanbul", result.OutputJson);
        Assert.Contains("23", result.OutputJson);
    }

    // Verifies that tool names are resolved without casing sensitivity.
    [Fact]
    public async Task InvokeAsync_ShouldResolveToolNameCaseInsensitive_WhenToolNameUsesDifferentCasing()
    {
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "WEATHER",
            """{"city":"Istanbul"}""");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("Istanbul", result.OutputJson);
    }

    // Verifies that empty argument JSON is treated as an empty JSON object.
    [Fact]
    public async Task InvokeAsync_ShouldUseEmptyObject_WhenArgumentsJsonIsEmpty()
    {
        var agent = CreateAgent()
            .AddTool<DefaultInputTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "default_input",
            "");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("default-city", result.OutputJson);
    }

    // Verifies that empty tool names return a failure result before execution is attempted.
    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolNameIsEmpty()
    {
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "",
            """{"city":"Istanbul"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolNameRequired", result.ErrorCode);
        Assert.Equal("Tool name cannot be empty.", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    // Verifies that calls to tools not registered on the agent return ToolNotFound.
    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolIsNotRegisteredOnAgent()
    {
        var agent = CreateAgent();
        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            """{"city":"Istanbul"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolNotFound", result.ErrorCode);
        Assert.Contains("does not have a tool named 'weather'", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    // Verifies that invalid argument JSON returns a ToolInputInvalid failure.
    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenArgumentsJsonIsInvalid()
    {
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            "{ invalid-json");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolInputInvalid", result.ErrorCode);
        Assert.Equal("Tool 'weather' input has an invalid format.", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    // Verifies that exceptions thrown by tools are converted into ToolExecutionFailed results.
    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolThrowsException()
    {
        var agent = CreateAgent()
            .AddTool<FailingTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "failing",
            """{"value":"test"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolExecutionFailed", result.ErrorCode);
        Assert.Equal("The tool could not be executed.", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    private static Agent CreateAgent()
    {
        return new Agent(
            id: "test-agent",
            name: "Test Agent",
            instructions: "Test instructions.",
            model: "openai/gpt-5",
            apiKey: "test-key");
    }

    private static AgentToolInvoker CreateInvoker()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        return new AgentToolInvoker(serviceProvider);
    }

    [RuniqTool(
        name: "weather",
        description: "Gets current weather information for a city.")]
    private sealed class TestWeatherTool : IRuniqTool<TestWeatherInput, TestWeatherOutput>
    {
        public Task<TestWeatherOutput> ExecuteAsync(
            TestWeatherInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestWeatherOutput(
                City: input.City,
                TemperatureCelsius: 23));
        }
    }

    [RuniqTool(
        name: "default_input",
        description: "Uses default input values.")]
    private sealed class DefaultInputTool : IRuniqTool<DefaultInput, DefaultInputOutput>
    {
        public Task<DefaultInputOutput> ExecuteAsync(
            DefaultInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DefaultInputOutput(input.City));
        }
    }

    [RuniqTool(
        name: "failing",
        description: "Throws an exception for test purposes.")]
    private sealed class FailingTool : IRuniqTool<FailingInput, FailingOutput>
    {
        public Task<FailingOutput> ExecuteAsync(
            FailingInput input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Tool failed intentionally.");
        }
    }

    private sealed record TestWeatherInput(string City);

    private sealed record TestWeatherOutput(
        string City,
        int TemperatureCelsius);

    private sealed record DefaultInput(string City = "default-city");

    private sealed record DefaultInputOutput(string City);

    private sealed record FailingInput(string Value);

    private sealed record FailingOutput(string Value);
}

