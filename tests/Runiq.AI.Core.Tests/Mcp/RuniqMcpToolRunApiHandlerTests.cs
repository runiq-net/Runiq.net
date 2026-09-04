using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.Mcp;

namespace Runiq.AI.Core.Tests.Mcp
{
    /// <summary>
    /// Verifies MCP dashboard tool input binding and invocation boundaries.
    /// </summary>
    public sealed class RuniqMcpToolRunApiHandlerTests
    {
        // Missing required parameters must produce validation details without invoking the tool.
        [Fact]
        public async Task RunAsync_WhenRequiredParametersAreMissing_ReturnsBadRequestWithoutInvocation()
        {
            RequiredInputTool.Reset();
            var handler = CreateHandler();

            var result = await handler.RunAsync(
                "required_input",
                new RuniqMcpToolRunRequest(ParseInput("{}")),
                CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
            var problem = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            Assert.Contains("name", problem.Errors.Keys);
            Assert.Contains("count", problem.Errors.Keys);
            Assert.Equal(0, RequiredInputTool.InvocationCount);
        }

        // Explicit null for a non-nullable parameter must be rejected before tool activation or invocation.
        [Fact]
        public async Task RunAsync_WhenRequiredParameterIsNull_ReturnsBadRequestWithoutInvocation()
        {
            RequiredInputTool.Reset();
            var handler = CreateHandler();

            var result = await handler.RunAsync(
                "required_input",
                new RuniqMcpToolRunRequest(ParseInput("""{"name":null,"count":2}""")),
                CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
            var problem = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            Assert.Contains("name", problem.Errors.Keys);
            Assert.Equal(0, RequiredInputTool.InvocationCount);
        }

        // Values with an incompatible JSON type must produce a format validation error without invoking the tool.
        [Fact]
        public async Task RunAsync_WhenParameterFormatIsInvalid_ReturnsBadRequestWithoutInvocation()
        {
            RequiredInputTool.Reset();
            var handler = CreateHandler();

            var result = await handler.RunAsync(
                "required_input",
                new RuniqMcpToolRunRequest(ParseInput("""{"name":"sample","count":"invalid"}""")),
                CancellationToken.None);

            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
            var problem = Assert.IsType<HttpValidationProblemDetails>(valueResult.Value);

            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            Assert.Contains("count", problem.Errors.Keys);
            Assert.Equal(0, RequiredInputTool.InvocationCount);
        }

        private static RuniqMcpToolRunApiHandler CreateHandler()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            return new RuniqMcpToolRunApiHandler(services);
        }

        private static JsonElement ParseInput(string json) =>
            JsonDocument.Parse(json).RootElement.Clone();
    }

    [ModelContextProtocol.Server.McpServerToolType]
    internal sealed class RequiredInputTool
    {
        public static int InvocationCount { get; private set; }

        [ModelContextProtocol.Server.McpServerTool(Name = "required_input")]
        public static string Run(string name, int count, string? note = null)
        {
            InvocationCount++;
            return $"{name}:{count}:{note}";
        }

        public static void Reset() => InvocationCount = 0;
    }
}

namespace ModelContextProtocol.Server
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class McpServerToolTypeAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class McpServerToolAttribute : Attribute
    {
        public string? Name { get; init; }
    }
}
