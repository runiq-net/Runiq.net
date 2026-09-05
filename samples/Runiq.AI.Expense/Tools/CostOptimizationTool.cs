using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Estimates preferred-vendor consolidation savings.</summary>
[RuniqTool(name: "cost_optimization", description: "Estimates an 8% savings scenario for spend moved to a preferred vendor.")]
public sealed class CostOptimizationTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public CostOptimizationTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Estimates savings.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Savings findings.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preferred = data.Vendors.Values.Where(value => value.Preferred).ToDictionary(value => value.ConsolidationGroup, StringComparer.OrdinalIgnoreCase);
        var findings = ExpenseToolSupport.Filter(data, input, includeExpenseId: false)
            .Where(item => !data.Vendors[item.VendorId].Preferred && preferred.ContainsKey(data.Vendors[item.VendorId].ConsolidationGroup))
            .GroupBy(item => data.Vendors[item.VendorId].ConsolidationGroup, StringComparer.OrdinalIgnoreCase)
            .Where(group => string.IsNullOrWhiteSpace(input.ExpenseId) || group.Any(item => item.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase)))
            .Select(group =>
            {
                var target = preferred[group.Key];
                var spend = group.Sum(item => ExpenseToolSupport.Normalize(data, item));
                var saving = decimal.Round(spend * 0.08m, 2);
                return new ExpenseEvidence($"{group.Key}:{target.Id}", string.Join(',', group.Select(item => item.Id)), "PREFERRED-VENDOR-8-PERCENT", "Illustrative 8% consolidation scenario; not an observed price difference.", saving, data.ReportingCurrency, null, string.Join("; ", group.Take(100).Select(item => $"{item.SourceFile}:{item.SourceRow}")), group.Count(), group.Count() > 100, TargetVendorId: target.Id, TargetVendorName: target.Name, ConsolidationGroup: group.Key, CurrentSpend: spend, SavingsRate: 0.08m, EstimatedSavings: saving, Assumption: "Move eligible spend to the preferred vendor and apply an illustrative 8% discount.");
            }).ToArray();
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Found {findings.Length} savings opportunities.", findings, findings.Sum(value => value.ReportingAmount)));
    }
}
