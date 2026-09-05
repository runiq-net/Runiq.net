using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Tools;

namespace Runiq.AI.Expense.Agents;

/// <summary>Defines the corporate expense analyst agent and its deterministic tool set.</summary>
public static class ExpenseAnalystAgent
{
    /// <summary>Creates the configured expense analyst agent.</summary>
    /// <param name="apiKey">Optional OpenAI API key supplied by configuration.</param>
    /// <returns>The configured agent.</returns>
    public static Agent Create(string? apiKey) => new Agent(
        id: "corporate-expense-analyst",
        name: "Corporate Expense Analyst",
        instructions: """
        You analyze the synthetic corporate expense workbooks only through the available typed tools.
        Select the tool that directly matches the user's question. Never calculate totals, currency conversions,
        policy breaches, duplicate matches, anomalies, or savings yourself. Treat tool output as the sole source
        of financial records and arithmetic. Preserve expense IDs, rules, thresholds, original currency evidence,
        reporting currency, and source references when explaining findings. Do not invent missing records.
        An empty result is successful: clearly say no matching records were found. Savings are estimates based on
        the stated assumption, not commitments or financial advice.
        """,
        model: "openai/gpt-5",
        apiKey: apiKey)
        .AddTool<ExpenseSearchTool>()
        .AddTool<BudgetVarianceTool>()
        .AddTool<PolicyViolationTool>()
        .AddTool<DuplicateExpenseTool>()
        .AddTool<AnomalousExpenseTool>()
        .AddTool<CostOptimizationTool>();
}
