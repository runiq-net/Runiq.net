using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Calculates monthly department budget variance.</summary>
[RuniqTool(name: "budget_variance", description: "Compares monthly department expenses with budget in reporting currency.")]
public sealed class BudgetVarianceTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public BudgetVarianceTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Calculates budget variance.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Budget findings.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = ExpenseToolSupport.Filter(data, input, includeExpenseId: false);
        if (!string.IsNullOrWhiteSpace(input.ExpenseId))
        {
            var target = data.Transactions.FirstOrDefault(item => item.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase));
            rows = target is null
                ? []
                : rows.Where(item => item.DepartmentId.Equals(target.DepartmentId, StringComparison.OrdinalIgnoreCase) && item.ExpenseDate.Year == target.ExpenseDate.Year && item.ExpenseDate.Month == target.ExpenseDate.Month).ToArray();
        }
        var findings = rows
            .GroupBy(item => (item.DepartmentId, Period: new DateOnly(item.ExpenseDate.Year, item.ExpenseDate.Month, 1)))
            .Select(group =>
            {
                var actual = group.Sum(item => ExpenseToolSupport.Normalize(data, item));
                var budget = data.Budgets.Single(item => item.DepartmentId.Equals(group.Key.DepartmentId, StringComparison.OrdinalIgnoreCase) && item.Period == group.Key.Period).Amount;
                var variance = actual - budget;
                return new ExpenseEvidence($"{group.Key.DepartmentId}:{group.Key.Period:yyyy-MM}", string.Join(',', group.Select(item => item.Id)), "BUDGET-VARIANCE", "Actual minus budget.", variance, data.ReportingCurrency, null, string.Join("; ", group.Select(item => $"{item.SourceFile}:{item.SourceRow}")), group.Count(), false, actual, budget, variance, variance >= 0 ? "OverBudget" : "UnderBudget");
            }).OrderByDescending(item => Math.Abs(item.ReportingAmount)).ToArray();
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Calculated {findings.Length} budget variances.", findings, findings.Sum(value => value.ReportingAmount)));
    }
}
