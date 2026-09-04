using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Agents;
using Runiq.AI.Core;
using Runiq.AI.Workflows;

namespace Runiq.AI.Core.Tests.Dashboard;

/// <summary>
/// Dashboard workflow metadata endpoint davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardFlowEndpointTests
{
    [Fact]
    public async Task FlowsEndpoint_ShouldReturnRegisteredFlows()
    {
        // Flow liste endpoint'inin registry'deki workflow tanımlarını döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/api/workflows");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var workflows = document.RootElement;

        Assert.Equal(JsonValueKind.Array, workflows.ValueKind);

        var workflow = Assert.Single(workflows.EnumerateArray());

        Assert.Equal("travel-planning-workflow", workflow.GetProperty("id").GetString());
        Assert.Equal("Travel Planning Flow", workflow.GetProperty("name").GetString());

        var steps = workflow.GetProperty("steps");

        Assert.Equal(3, steps.GetArrayLength());
        Assert.Equal("weather", steps[0].GetProperty("id").GetString());
        Assert.Equal(nameof(WeatherAgent), steps[0].GetProperty("agentName").GetString());
        Assert.Equal("places", steps[0].GetProperty("successStepId").GetString());
        Assert.Equal("Continue", steps[0].GetProperty("failureBehavior").GetString());
        Assert.Equal("places", steps[0].GetProperty("failureStepId").GetString());
    }

    [Fact]
    public async Task FlowDetailEndpoint_ShouldReturnFlowById()
    {
        // Flow detay endpoint'inin id ile istenen workflow metadata bilgisini döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server
            .GetTestClient()
            .GetAsync("/dashboard/api/workflows/travel-planning-workflow");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "travel-planning-workflow",
            document.RootElement.GetProperty("id").GetString());
        Assert.Equal("weather", document.RootElement.GetProperty("startStepId").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("stepCount").GetInt32());
    }

    [Fact]
    public async Task FlowDetailEndpoint_ShouldReturnNotFound_WhenFlowIdIsUnknown()
    {
        // Bilinmeyen workflow id için detay endpoint'inin 404 döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server
            .GetTestClient()
            .GetAsync("/dashboard/api/workflows/missing-workflow");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FlowRunEndpoint_ShouldReturnExecutionResult()
    {
        // Flow run endpoint'inin runtime sonucunu dashboard DTO'suna map ettiğini doğrular.
        var runtime = new DashboardTestFlowRuntime();
        using var server = CreateServer(runtime);

        var response = await server
            .GetTestClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/travel-planning-workflow/run",
                new { input = "Istanbul trip" });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var steps = root.GetProperty("steps");

        Assert.True(runtime.WasCalled);
        Assert.Equal("travel-planning-workflow", runtime.FlowId);
        Assert.Equal("Istanbul trip", runtime.Input);
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal("Planner output", root.GetProperty("finalOutput").GetString());
        Assert.Equal("planner", steps[0].GetProperty("stepId").GetString());
        Assert.Equal("PlannerAgent", steps[0].GetProperty("agentName").GetString());
        Assert.Equal("Istanbul trip", steps[0].GetProperty("input").GetString());
        Assert.Equal("Planner output", steps[0].GetProperty("output").GetString());

        var toolCalls = steps[0].GetProperty("toolCalls");
        var toolCall = Assert.Single(toolCalls.EnumerateArray());

        Assert.Equal("weather.search", toolCall.GetProperty("toolName").GetString());
        Assert.Equal("Completed", toolCall.GetProperty("status").GetString());
        Assert.Equal("""{"city":"Istanbul"}""", toolCall.GetProperty("argumentsJson").GetString());
        Assert.Equal("""{"condition":"clear"}""", toolCall.GetProperty("outputJson").GetString());
    }

    [Fact]
    public async Task FlowRunEndpoint_ShouldReturnNotFound_WhenFlowIdIsUnknown()
    {
        // Bilinmeyen workflow id için run endpoint'inin 404 döndürdüğünü doğrular.
        using var server = CreateServer(new DashboardTestFlowRuntime());

        var response = await server
            .GetTestClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/missing-workflow/run",
                new { input = "Istanbul trip" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FlowRunEndpoint_ShouldReturnBadRequest_WhenInputIsEmpty()
    {
        // Boş workflow girdisinin runtime çalıştırılmadan reddedildiğini doğrular.
        var runtime = new DashboardTestFlowRuntime();
        using var server = CreateServer(runtime);

        var response = await server
            .GetTestClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/travel-planning-workflow/run",
                new { input = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(runtime.WasCalled);
    }

    [Fact]
    public async Task TeamApiEndpoint_ShouldReturnNotFound()
    {
        // Agent Team API endpoint'inin aktif dashboard API yüzeyinden kaldırıldığını doğrular.
        using var server = CreateServer();

        var response = await server
            .GetTestClient()
            .PostAsync("/dashboard/api/teams/travel-team/chat", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void FlowServices_ShouldBuildWithScopeValidationEnabled()
    {
        // Flow runtime servislerinin scoped agent runtime ile uyumlu lifetime kullandığını doğrular.
        using var host = new HostBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureWebHost(webBuilder => webBuilder
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRuniqServer(options =>
                {
                    options.AddAgent(new WeatherAgent());
                    options.AddAgent(new PlacesAgent());
                    options.AddAgent(new PlannerAgent());
                });
                services.AddRuniqWorkflows(options => options.AddFlow(CreateFlow()));
            })
            .Configure(app => { }))
            .Start();

        using var scope = host.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFlowRunner>());
    }

    private static IHost CreateServer(
        IFlowRunner? workflowExecutionRuntime = null)
    {
        PrepareDashboardAssets();

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseContentRoot(AppContext.BaseDirectory)
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddRuniqServer();
                        services.AddRuniqWorkflows(options =>
                        {
                            options.AddFlow(CreateFlow());
                        });

                        if (workflowExecutionRuntime is not null)
                        {
                            services.AddScoped(_ => workflowExecutionRuntime);
                        }
                    })
                    .Configure(app =>
                    {
                        app.UseRuniqDashboard(options =>
                        {
                            options.Path = "/dashboard";
                            options.Title = "Test Dashboard";
                            options.Authentication(auth => auth.AllowAnonymous());
                        });
                    });
            })
            .Start();
    }

    private static Flow CreateFlow()
    {
        return new Flow(
                id: "travel-planning-workflow",
                name: "Travel Planning Flow")
            .Step<WeatherAgent>("weather")
                .OnSuccess("places")
                .OnFailureContinue("places")
            .Step<PlacesAgent>("places")
                .OnSuccess("planner")
                .OnFailureContinue("planner")
            .Step<PlannerAgent>("planner")
                .OnFailureStop()
            .Build();
    }

    private static void PrepareDashboardAssets()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        Directory.CreateDirectory(root);

        var indexPath = Path.Combine(root, "index.html");

        File.WriteAllText(
            indexPath,
            """
            <!doctype html>
            <html>
            <head>
                <title>__RUNIQ_TITLE_HTML__</title>
                <script>
                    window.__RUNIQ_DASHBOARD__ = __RUNIQ_DASHBOARD_CONFIG__;
                </script>
            </head>
            <body>Runiq Dashboard</body>
            </html>
            """);
    }

    private sealed class WeatherAgent : Agent
    {
        public WeatherAgent()
            : base("weather-agent", "Weather Agent", "Weather.", "openai/gpt-5")
        {
        }
    }

    private sealed class PlacesAgent : Agent
    {
        public PlacesAgent()
            : base("places-agent", "Places Agent", "Places.", "openai/gpt-5")
        {
        }
    }

    private sealed class PlannerAgent : Agent
    {
        public PlannerAgent()
            : base("planner-agent", "Planner Agent", "Plan.", "openai/gpt-5")
        {
        }
    }

    private sealed class DashboardTestFlowRuntime : IFlowRunner
    {
        public bool WasCalled { get; private set; }

        public string? FlowId { get; private set; }

        public string? Input { get; private set; }

        public Task<RunResult> ExecuteAsync(
            Flow workflow,
            string input,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            FlowId = workflow.Id;
            Input = input;
            var startedAt = DateTimeOffset.Parse("2026-05-27T10:00:00+03:00");
            var completedAt = startedAt.AddMilliseconds(42);

            return Task.FromResult(new RunResult(
                RunStatus.Completed,
                [
                    new StepRunResult(
                        stepId: "planner",
                        agentType: typeof(PlannerAgent),
                        status: StepRunStatus.Completed,
                        input: input,
                        output: "Planner output",
                        toolCalls:
                        [
                            new ToolCallRunResult(
                                toolCallId: "call_weather",
                                toolName: "weather.search",
                                status: ToolCallRunStatus.Completed,
                                argumentsJson: """{"city":"Istanbul"}""",
                                outputJson: """{"condition":"clear"}""",
                                startedAt: startedAt,
                                completedAt: completedAt)
                        ])
                ],
                finalOutput: "Planner output"));
        }
    }
}

