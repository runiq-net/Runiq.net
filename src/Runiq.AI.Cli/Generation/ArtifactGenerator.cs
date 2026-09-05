using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

public sealed class ArtifactGenerator
{
    private readonly IFileSystem _fileSystem;

    public ArtifactGenerator(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void Generate(ProjectDefinition definition)
    {
        if (!definition.IncludeSampleCode)
        {
            return;
        }

        var apiProjectRoot = Path.Combine(
            definition.Name,
            "src",
            $"{definition.Name}.Api");

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Agents", "WeatherAgent.cs"),
            CreateWeatherAgentContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Agents", "PlacesAgent.cs"),
            CreatePlacesAgentContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Agents", "PlannerAgent.cs"),
            CreatePlannerAgentContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Tools", "WeatherTool.cs"),
            CreateWeatherToolContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Tools", "PlacesTool.cs"),
            CreatePlacesToolContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Tools", "MealSuggestionTool.cs"),
            CreateMealSuggestionToolContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Flows", "TravelPlanningFlow.cs"),
            CreateWorkflowContent(definition));

        if (definition.EnableMcp)
        {
            _fileSystem.WriteAllText(
                Path.Combine(apiProjectRoot, "Mcp", "README.md"),
                CreateMcpReadmeContent(definition));
        }
    }

    private static string CreateWeatherAgentContent(ProjectDefinition definition)
    {
        return $$""""
               using Runiq.AI.Agents;
               using Runiq.AI.Agents.Tools;
               using {{definition.Name}}.Api.Tools;

               namespace {{definition.Name}}.Api.Agents;

               /// <summary>Defines the weather specialist used by the travel workflow.</summary>
               public sealed class WeatherAgent : Agent
               {
                   private WeatherAgent(string? apiKey)
                       : base(
                           id: "weather-agent",
                           name: "Weather Agent",
                           instructions: """
                           Analyze weather and travel comfort. Always use WeatherTool and return a concise contribution for the next workflow step.
                           Answer in the same language as the user. Do not create the final itinerary.
                           """,
                           model: "openai/gpt-5",
                           apiKey: apiKey)
                   {
                   }

                   /// <summary>Creates the weather agent with its typed tool.</summary>
                   public static Agent Create(string? apiKey) => new WeatherAgent(apiKey).AddTool<WeatherTool>();
               }
               """";
    }

    private static string CreatePlacesAgentContent(ProjectDefinition definition)
    {
        return $$""""
               using Runiq.AI.Agents;
               using Runiq.AI.Agents.Tools;
               using {{definition.Name}}.Api.Tools;

               namespace {{definition.Name}}.Api.Agents;

               /// <summary>Defines the places specialist used by the travel workflow.</summary>
               public sealed class PlacesAgent : Agent
               {
                   private PlacesAgent(string? apiKey)
                       : base(
                           id: "places-agent",
                           name: "Places Agent",
                           instructions: """
                           Suggest practical and walkable places. Always use PlacesTool and return a concise contribution for the final planner.
                           Use previous workflow output as context. Answer in the same language as the user. Do not create the final itinerary.
                           """,
                           model: "openai/gpt-5",
                           apiKey: apiKey)
                   {
                   }

                   /// <summary>Creates the places agent with its typed tool.</summary>
                   public static Agent Create(string? apiKey) => new PlacesAgent(apiKey).AddTool<PlacesTool>();
               }
               """";
    }

    private static string CreatePlannerAgentContent(ProjectDefinition definition)
    {
        return $$""""
               using Runiq.AI.Agents;
               using Runiq.AI.Agents.Tools;
               using {{definition.Name}}.Api.Tools;

               namespace {{definition.Name}}.Api.Agents;

               /// <summary>Defines the final planner used by the travel workflow.</summary>
               public sealed class PlannerAgent : Agent
               {
                   private PlannerAgent(string? apiKey)
                       : base(
                           id: "planner-agent",
                           name: "Planner Agent",
                           instructions: """
                           Create the final practical itinerary from the user's request and previous workflow outputs.
                           Always use MealSuggestionTool before answering. Include weather, route flow, breaks, and meal areas.
                           Answer in the same language as the user and do not expose raw tool output.
                           """,
                           model: "openai/gpt-5",
                           apiKey: apiKey)
                   {
                   }

                   /// <summary>Creates the planner agent with its typed tool.</summary>
                   public static Agent Create(string? apiKey) => new PlannerAgent(apiKey).AddTool<MealSuggestionTool>();
               }
               """";
    }

    private static string CreateWeatherToolContent(ProjectDefinition definition)
    {
        var mcpUsingStatements = definition.EnableMcp
            ? "using System.ComponentModel;\nusing ModelContextProtocol.Server;\n"
            : string.Empty;
        var mcpTypeAttribute = definition.EnableMcp
            ? "[McpServerToolType]\n"
            : string.Empty;
        var mcpMethodAttributes = definition.EnableMcp
            ? """
                  [McpServerTool(Name = "weather.get", ReadOnly = true)]
                  [Description("Gets starter sample weather for a city.")]
              """
            : string.Empty;
        var mcpParameterAttribute = definition.EnableMcp
            ? "[Description(\"The city to check.\")] "
            : string.Empty;

        return $$"""
               {{mcpUsingStatements}}using Runiq.AI.Agents.Tools;

               namespace {{definition.Name}}.Api.Tools;

               [RuniqTool(
                   name: "weather_get",
                   description: "Gets starter sample weather for a city.")]
               {{mcpTypeAttribute}}public sealed class WeatherTool : IRuniqTool<WeatherToolInput, WeatherToolOutput>
               {
               {{mcpMethodAttributes}}
                   public string GetWeather({{mcpParameterAttribute}}string city)
                   {
                       return $"{city} weather is mild and partly cloudy, around 18 C.";
                   }

                   public Task<WeatherToolOutput> ExecuteAsync(
                       WeatherToolInput input,
                       CancellationToken cancellationToken = default)
                   {
                       return Task.FromResult(new WeatherToolOutput(
                           City: input.City,
                           Forecast: GetWeather(input.City)));
                   }
               }

               public sealed record WeatherToolInput(string City);

               public sealed record WeatherToolOutput(
                   string City,
                   string Forecast);
               """;
    }

    private static string CreatePlacesToolContent(ProjectDefinition definition)
    {
        var mcpUsingStatements = definition.EnableMcp
            ? "using System.ComponentModel;\nusing ModelContextProtocol.Server;\n"
            : string.Empty;
        var mcpTypeAttribute = definition.EnableMcp
            ? "[McpServerToolType]\n"
            : string.Empty;
        var mcpMethodAttributes = definition.EnableMcp
            ? """
                  [McpServerTool(Name = "places.get", ReadOnly = true)]
                  [Description("Gets walkable starter sample places for a city.")]
              """
            : string.Empty;
        var cityDescription = definition.EnableMcp
            ? "[Description(\"The city to explore.\")] "
            : string.Empty;

        return $$"""
               {{mcpUsingStatements}}using Runiq.AI.Agents.Tools;

               namespace {{definition.Name}}.Api.Tools;

               [RuniqTool(
                   name: "places",
                   description: "Gets walkable starter sample places for a city.")]
               {{mcpTypeAttribute}}public sealed class PlacesTool : IRuniqTool<PlacesToolInput, PlacesToolOutput>
               {
               {{mcpMethodAttributes}}
                   public IReadOnlyList<string> GetPlaces({{cityDescription}}string city)
                   {
                       return city.Trim().ToUpperInvariant() switch
                       {
                           "ISTANBUL" => ["Sultanahmet Square", "Gulhane Park", "Karakoy"],
                           "IZMIR" => ["Konak Square", "Kemeralti", "Kordon"],
                           _ => ["City center", "Old town", "Main square"]
                       };
                   }

                   public Task<PlacesToolOutput> ExecuteAsync(
                       PlacesToolInput input,
                       CancellationToken cancellationToken = default)
                   {
                       return Task.FromResult(new PlacesToolOutput(input.City, GetPlaces(input.City)));
                   }
               }

               public sealed record PlacesToolInput(string City);

               public sealed record PlacesToolOutput(string City, IReadOnlyList<string> Places);
               """;
    }

    private static string CreateMealSuggestionToolContent(ProjectDefinition definition)
    {
        var mcpUsingStatements = definition.EnableMcp
            ? "using System.ComponentModel;\nusing ModelContextProtocol.Server;\n"
            : string.Empty;
        var mcpTypeAttribute = definition.EnableMcp
            ? "[McpServerToolType]\n"
            : string.Empty;
        var mcpMethodAttributes = definition.EnableMcp
            ? """
                  [McpServerTool(Name = "meal.suggest", ReadOnly = true)]
                  [Description("Gets starter sample meal areas for a city.")]
              """
            : string.Empty;
        var cityDescription = definition.EnableMcp
            ? "[Description(\"The city for the meal suggestion.\")] "
            : string.Empty;

        return $$"""
               {{mcpUsingStatements}}using Runiq.AI.Agents.Tools;

               namespace {{definition.Name}}.Api.Tools;

               [RuniqTool(
                   name: "meal_suggestion",
                   description: "Gets starter sample meal areas for a city.")]
               {{mcpTypeAttribute}}public sealed class MealSuggestionTool : IRuniqTool<MealSuggestionToolInput, MealSuggestionToolOutput>
               {
               {{mcpMethodAttributes}}
                   public string GetMealAreas({{cityDescription}}string city) =>
                       city.Trim().Equals("Istanbul", StringComparison.OrdinalIgnoreCase)
                           ? "Lunch near Sultanahmet; dinner in Karakoy or Galata."
                           : "Choose meal areas close to the walking route.";

                   public Task<MealSuggestionToolOutput> ExecuteAsync(
                       MealSuggestionToolInput input,
                       CancellationToken cancellationToken = default)
                   {
                       return Task.FromResult(new MealSuggestionToolOutput(input.City, GetMealAreas(input.City)));
                   }
               }

               public sealed record MealSuggestionToolInput(string City);

               public sealed record MealSuggestionToolOutput(string City, string Suggestion);
               """;
    }

    private static string CreateWorkflowContent(ProjectDefinition definition)
    {
        return $$"""
               using {{definition.Name}}.Api.Agents;
               using Runiq.AI.Workflows.Domain;

               namespace {{definition.Name}}.Api.Flows;

               /// <summary>Creates the deterministic travel planning workflow.</summary>
               public static class TravelPlanningFlow
               {
                   /// <summary>Builds the weather-to-places-to-planner flow.</summary>
                   public static Flow Create() => new Flow(
                           id: "travel-planning-workflow",
                           name: "Travel Planning Workflow")
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
               """;
    }

    private static string CreateMcpReadmeContent(ProjectDefinition definition)
    {
        return $$"""
               # MCP Starter Tools

               When MCP is enabled, this project references `Runiq.AI.Mcp` and maps `/mcp` in `Program.cs`.

               The starter travel tools include MCP metadata and can be exposed as:

               - `weather.get`
               - `places.get`
               - `meal.suggest`

               They mirror the same small sample capabilities used by the generated agents:

               - `WeatherTool.GetWeather("Istanbul")`
               - `PlacesTool.GetPlaces("Istanbul")`
               - `MealSuggestionTool.GetMealAreas("Istanbul")`
               """;
    }
}


