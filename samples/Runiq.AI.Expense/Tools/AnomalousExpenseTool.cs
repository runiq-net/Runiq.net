using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Finds unusually large expenses.</summary>
[RuniqTool(name: "anomalous_expenses", description: "Finds category z-score outliers and expenses of at least TRY 150,000.")]
public sealed class AnomalousExpenseTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public AnomalousExpenseTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Finds anomalous expenses.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Anomaly findings.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        var findings = new List<ExpenseEvidence>();
        foreach (var group in ExpenseToolSupport.Filter(data, input, includeExpenseId: false).GroupBy(item => item.CategoryId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = group.Select(item => ExpenseToolSupport.Normalize(data, item)).ToArray();
            var mean = values.Average();
            var deviation = (decimal)Math.Sqrt(values.Select(value => Math.Pow((double)(value - mean), 2)).Average());
            foreach (var item in group)
            {
                if (!string.IsNullOrWhiteSpace(input.ExpenseId) && !item.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase)) continue;
                var amount = ExpenseToolSupport.Normalize(data, item);
                var score = deviation == 0 ? 0 : (amount - mean) / deviation;
                var statisticalOutlier = values.Length >= 5 && score >= 2.5m;
                if (statisticalOutlier || amount >= 150_000m)
                {
                    var scoreEvidence = values.Length >= 5 ? $"z-score {score:N2}" : "z-score skipped because the filtered category has fewer than five records";
                    findings.Add(ExpenseToolSupport.Evidence(data, item, "ZSCORE-2.5-OR-150K", $"Amount {amount:N2}; category mean {mean:N2}; {scoreEvidence}; fixed threshold 150,000 {data.ReportingCurrency}."));
                }
            }
        }
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Found {findings.Count} anomalous expenses.", findings, findings.Sum(value => value.ReportingAmount)));
    }
}
