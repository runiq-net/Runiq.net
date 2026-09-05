using Runiq.AI.Cli.Generation;
using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Tests;

public sealed class CliSampleGenerationTests
{
    [Fact]
    // Verifies the CLI emits the same agent, tool, and flow structure as the maintained workflow sample.
    public void ArtifactGenerator_ShouldGenerateCurrentWorkflowSampleStructure()
    {
        var fileSystem = new RecordingFileSystem();
        var definition = CreateDefinition();

        new ArtifactGenerator(fileSystem).Generate(definition);

        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Agents", "WeatherAgent.cs"), StringComparison.Ordinal));
        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Agents", "PlacesAgent.cs"), StringComparison.Ordinal));
        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Agents", "PlannerAgent.cs"), StringComparison.Ordinal));
        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Tools", "WeatherTool.cs"), StringComparison.Ordinal));
        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Tools", "PlacesTool.cs"), StringComparison.Ordinal));
        Assert.Contains(fileSystem.Files.Keys, path => path.EndsWith(Path.Combine("Tools", "MealSuggestionTool.cs"), StringComparison.Ordinal));

        var flow = Assert.Single(fileSystem.Files, file => file.Key.EndsWith(Path.Combine("Flows", "TravelPlanningFlow.cs"), StringComparison.Ordinal)).Value;
        Assert.Contains("new Flow(", flow, StringComparison.Ordinal);
        Assert.Contains(".Step<WeatherAgent>(\"weather\")", flow, StringComparison.Ordinal);
        Assert.Contains(".Step<PlacesAgent>(\"places\")", flow, StringComparison.Ordinal);
        Assert.Contains(".Step<PlannerAgent>(\"planner\")", flow, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", flow, StringComparison.Ordinal);
        Assert.DoesNotContain(fileSystem.Files.Keys, path => path.Contains("TravelPlannerAgent", StringComparison.Ordinal));
        Assert.DoesNotContain(fileSystem.Files.Keys, path => path.Contains("TripCostTool", StringComparison.Ordinal));
    }

    [Fact]
    // Verifies sample-enabled projects install and register the real Runiq workflow runtime.
    public void IntegrationGenerator_ShouldInstallAndRegisterWorkflowPackage()
    {
        var fileSystem = new RecordingFileSystem();
        var processRunner = new RecordingProcessRunner();
        var definition = CreateDefinition();
        var projectPath = Path.Combine("Demo", "src", "Demo.Api", "Demo.Api.csproj");

        new RuniqIntegrationGenerator(fileSystem, processRunner).Generate(definition, projectPath);

        Assert.Contains(processRunner.Calls, call => call.Arguments.Contains(RuniqPackageNames.Workflows));
        var program = fileSystem.Files[Path.Combine("Demo", "src", "Demo.Api", "Program.cs")];
        Assert.Contains("AddRuniqWorkflows", program, StringComparison.Ordinal);
        Assert.Contains("TravelPlanningFlow.Create()", program, StringComparison.Ordinal);
        Assert.Contains("WeatherAgent.Create(openAiApiKey)", program, StringComparison.Ordinal);
        Assert.Contains("PlacesAgent.Create(openAiApiKey)", program, StringComparison.Ordinal);
        Assert.Contains("PlannerAgent.Create(openAiApiKey)", program, StringComparison.Ordinal);
    }

    private static ProjectDefinition CreateDefinition() => new()
    {
        Name = "Demo",
        Provider = AiProvider.OpenAi,
        IncludeSampleCode = true,
        EnableDashboard = true,
        EnableMcp = false,
        ApiKeySetupMode = ApiKeySetupMode.Skip
    };

    private sealed class RecordingFileSystem : IFileSystem
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

        public void CreateDirectory(string path)
        {
        }

        public string ReadAllText(string path) => Files[path];

        public void WriteAllText(string path, string content) => Files[path] = content;
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessCall> Calls { get; } = [];

        public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
        {
            Calls.Add(new ProcessCall(fileName, arguments.ToArray(), workingDirectory));
            return new ProcessResult { ExitCode = 0 };
        }
    }

    private sealed record ProcessCall(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);
}
