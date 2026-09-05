using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

/// <summary>Generates Runiq package, host, security, and controller integration for a CLI-created API project.</summary>
public sealed class RuniqIntegrationGenerator
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    /// <summary>Creates a generator backed by the supplied filesystem and process abstractions.</summary>
    /// <param name="fileSystem">Filesystem used to create generated artifacts.</param>
    /// <param name="processRunner">Process runner used for .NET project operations.</param>
    public RuniqIntegrationGenerator(
        IFileSystem fileSystem,
        IProcessRunner processRunner)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
    }

    /// <summary>Applies the selected project definition to an API project.</summary>
    /// <param name="definition">Selected Runiq project features and provider settings.</param>
    /// <param name="apiProjectPath">Path to the target API project file.</param>
    public void Generate(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        AddPackages(definition, apiProjectPath);
        ConfigureUserSecrets(definition, apiProjectPath);
        UpdateProgram(definition, apiProjectPath);
        WriteStatusController(definition, apiProjectPath);
    }

    private void WriteStatusController(ProjectDefinition definition, string apiProjectPath)
    {
        var projectDirectory = Path.GetDirectoryName(apiProjectPath)
            ?? throw new InvalidOperationException("API project path has no parent directory.");
        var controllersDirectory = Path.Combine(projectDirectory, "Controllers");
        _fileSystem.CreateDirectory(controllersDirectory);
        _fileSystem.WriteAllText(Path.Combine(controllersDirectory, "StatusController.cs"), $$"""
            using Microsoft.AspNetCore.Mvc;

            namespace {{definition.Name}}.Api.Controllers;

            /// <summary>Reports whether the generated API host is running.</summary>
            [ApiController]
            [Route("")]
            public sealed class StatusController : ControllerBase
            {
                /// <summary>Returns the generated API status message.</summary>
                [HttpGet]
                public ActionResult<string> Get() => "{{definition.Name}} API is running.";
            }
            """);
    }

    private void AddPackages(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        RunDotNet(
            [
                "add",
                apiProjectPath,
                "package",
                RuniqPackageNames.Core,
                "--prerelease"
            ],
            Directory.GetCurrentDirectory());

        if (definition.IncludeSampleCode)
        {
            RunDotNet(
                [
                    "add",
                    apiProjectPath,
                    "package",
                    RuniqPackageNames.Workflows,
                    "--prerelease"
                ],
                Directory.GetCurrentDirectory());
        }

        if (definition.EnableMcp)
        {
            RunDotNet(
                [
                    "add",
                    apiProjectPath,
                    "package",
                    RuniqPackageNames.Mcp,
                    "--prerelease"
                ],
                Directory.GetCurrentDirectory());
        }
    }

    private void UpdateProgram(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        var programPath = Path.Combine(
            Path.GetDirectoryName(apiProjectPath)
                ?? throw new InvalidOperationException("API project path has no parent directory."),
            "Program.cs");

        _fileSystem.WriteAllText(
            programPath,
            CreateProgramContent(definition));
    }

    private void ConfigureUserSecrets(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        if (definition.ApiKeySetupMode != ApiKeySetupMode.UserSecrets)
        {
            return;
        }

        RunDotNet(
            [
                "user-secrets",
                "init",
                "--project",
                apiProjectPath
            ],
            Directory.GetCurrentDirectory());

        if (definition.Provider == AiProvider.AzureOpenAi)
        {
            SetUserSecret(
                apiProjectPath,
                "AzureOpenAI:Endpoint",
                definition.AzureOpenAiEndpoint
                    ?? throw new InvalidOperationException("Azure OpenAI endpoint is missing."));

            SetUserSecret(
                apiProjectPath,
                "AzureOpenAI:ApiKey",
                definition.ApiKeyValue
                    ?? throw new InvalidOperationException("Azure OpenAI API key is missing."));

            return;
        }

        SetUserSecret(
            apiProjectPath,
            GetApiKeyName(definition.Provider),
            definition.ApiKeyValue
                ?? throw new InvalidOperationException("Provider API key is missing."));
    }

    private void SetUserSecret(
        string apiProjectPath,
        string key,
        string value)
    {
        RunDotNet(
            [
                "user-secrets",
                "set",
                key,
                value,
                "--project",
                apiProjectPath
            ],
            Directory.GetCurrentDirectory(),
            [
                "user-secrets",
                "set",
                key,
                "<redacted>",
                "--project",
                apiProjectPath
            ]);
    }

    private static string CreateProgramContent(ProjectDefinition definition)
    {
        var usingStatements = new List<string>
        {
            "using Runiq.AI.Core;"
        };

        if (definition.IncludeSampleCode)
        {
            usingStatements.Add($"using {definition.Name}.Api.Agents;");
            usingStatements.Add($"using {definition.Name}.Api.Flows;");
            usingStatements.Add($"using {definition.Name}.Api.Tools;");
            usingStatements.Add("using Runiq.AI.Workflows;");
        }

        if (definition.EnableMcp)
        {
            usingStatements.Add("using Runiq.AI.Mcp;");
        }

        var mcpServices = definition.EnableMcp
            ? "\nbuilder.Services.AddRuniqMcp();"
            : string.Empty;

        var addRuniqServer = definition.IncludeSampleCode
            ? $$"""
              builder.Services.AddRuniqServer(options =>
              {
                  var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

                  options.AddAgent(WeatherAgent.Create(openAiApiKey));
                  options.AddAgent(PlacesAgent.Create(openAiApiKey));
                  options.AddAgent(PlannerAgent.Create(openAiApiKey));
                  options.AddTool<WeatherTool>();
                  options.AddTool<PlacesTool>();
                  options.AddTool<MealSuggestionTool>();
              });

              builder.Services.AddRuniqWorkflows(options =>
              {
                  options.AddFlow(TravelPlanningFlow.Create());
              });
              """
            : """
              builder.Services.AddRuniqServer();
              """;

        var dashboardMiddleware = definition.EnableDashboard
            ? $$"""

              app.UseRuniqDashboard(options =>
              {
                  options.Path = "/dashboard";
                  options.Title = "{{definition.Name}}";
                  options.Authentication(auth =>
                  {
                      if (app.Environment.IsDevelopment()) auth.AllowAnonymous();
                      else auth.RequireAuthenticatedUser();
                  });
              });
              """
            : string.Empty;

        var mcpEndpoints = definition.EnableMcp
            ? "\n\napp.MapRuniqMcp();"
            : string.Empty;

        return $$"""
               {{string.Join('\n', usingStatements)}}

               var builder = WebApplication.CreateBuilder(args);

               builder.Services.AddOpenApi();
               builder.Services.AddControllers();
               {{addRuniqServer}}{{mcpServices}}

               var app = builder.Build();

               if (app.Environment.IsDevelopment())
               {
                   app.MapOpenApi();
               }

               app.UseHttpsRedirection();

               app.MapControllers();{{dashboardMiddleware}}{{mcpEndpoints}}

               app.Run();
               """;
    }

    private static string GetApiKeyName(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => "OpenAI:ApiKey",
            AiProvider.Anthropic => "Anthropic:ApiKey",
            _ => throw new InvalidOperationException(
                $"Provider '{provider}' does not support API key setup.")
        };
    }

    private void RunDotNet(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyList<string>? displayArguments = null)
    {
        var result = _processRunner.Run(
            "dotnet",
            arguments,
            workingDirectory);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', displayArguments ?? arguments)} failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
        }
    }
}

