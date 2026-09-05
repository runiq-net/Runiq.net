# Runiq CLI

Runiq CLI creates ready-to-run ASP.NET Core projects for Runiq AI. It sets up a Visual Studio friendly solution, adds Runiq packages through NuGet, wires the generated API project, and can include a small starter sample with agents, typed tools, a real Runiq workflow, Dashboard, and MCP.

## Installation

Install the CLI as a .NET tool:

```powershell
dotnet tool install --global Runiq.AI.Cli --version 1.0.0
```

Update an existing installation:

```powershell
dotnet tool update --global Runiq.AI.Cli --version 1.0.0
```

Verify the command is available:

```powershell
runiq --help
runiq --version
```

## Create A Project

Run:

```powershell
runiq init
```

Or provide the project name up front:

```powershell
runiq init MyRuniqApp
```

The wizard asks for:

- Project name, when it is not provided in the command
- Default AI provider
- Provider API key setup
- Starter sample code
- Dashboard
- MCP

Supported provider choices:

- OpenAI
- Azure OpenAI
- Ollama
- Anthropic

You can skip API key setup during generation and configure it later. If you choose to enter a key, the CLI stores it with .NET user secrets for the generated API project.

## Generated Output

For a project named `Sample04`, the CLI creates a solution like this:

```text
Sample04/
  Sample04.sln
  README.md
  src/
    Sample04.Api/
      Sample04.Api.csproj
      Program.cs
      Agents/
      Tools/
      Flows/
      Mcp/
  tests/
```

The starter artifacts live inside `src/{ProjectName}.Api`, so they are visible directly under the API project in Visual Studio.

Generated projects use NuGet package references only. They do not use local Runiq source or project references.

## Run The Generated API

After generation:

```powershell
cd MyRuniqApp
dotnet run
```

If Dashboard is enabled, open:

```text
https://localhost:{port}/dashboard
```

If MCP is enabled, the MCP endpoint is available at:

```text
https://localhost:{port}/mcp
```

The CLI prints the detected Dashboard and MCP URLs when the generated launch settings expose an application URL.

## Starter Sample

When starter sample code is enabled, the generated project includes a compact version of the repository's `Runiq.AI.WorkflowTravelPlanner` scenario.

Try this in the Dashboard:

```text
Create a practical one-day historical trip plan in Istanbul for two people. Keep it easy to walk.
```

The sample includes:

- `WeatherAgent`
- `PlacesAgent`
- `PlannerAgent`
- `WeatherTool`
- `PlacesTool`
- `MealSuggestionTool`
- `TravelPlanningFlow.cs`
- `Mcp/README.md` when MCP is enabled

The conceptual flow is:

```text
WeatherAgent + WeatherTool
  -> PlacesAgent + PlacesTool
  -> PlannerAgent + MealSuggestionTool
  -> final itinerary
```

The sample is intentionally small. It is meant to show where agents, tools, prompts, and workflows belong without adding large datasets or complex business logic.

## Sample Tools

`WeatherTool` returns a simple hardcoded weather response:

```text
Istanbul weather is mild and partly cloudy, around 18 C.
```

`PlacesTool` returns a small deterministic set of walkable places, while `MealSuggestionTool` keeps meal stops close to the route.

```text
Sultanahmet Square -> Gulhane Park -> Karakoy
```

## MCP

When MCP is enabled, the generated project references `Runiq.AI.Mcp`, registers MCP services, and maps `/mcp`.

The starter tools include MCP metadata for:

- `weather.get`
- `places.get`
- `meal.suggest`

The generated `Mcp/README.md` explains the intended MCP tool examples.

## Generated Project README

The generated project README is user-facing. It gives prompt examples instead of documenting internal folder structure.

Example prompts:

```text
Create a practical one-day historical trip plan in Istanbul for two people. Keep it easy to walk.
```

```text
Plan the same trip for Izmir and account for the weather.
```

```text
Which agents and tools contributed to the result?
```

```text
Show the workflow trace for the Istanbul plan.
```

## Notes

- The CLI currently provides the `init` command.
- The wizard is interactive.
- Generated projects are intended to run immediately after creation.
- Starter workflow output is a registered `Runiq.AI.Workflows` flow, matching the repository sample architecture.
- The starter sample does not create helper, utility, service, CRM, order, or customer examples.

## Related Packages

Runiq AI is modular. Generated projects may use:

- `Runiq.AI.Core`
- `Runiq.AI.Workflows`
- `Runiq.AI.Mcp`
