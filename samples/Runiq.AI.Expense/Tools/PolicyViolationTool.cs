using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Finds expense policy violations.</summary>
[RuniqTool(name: "policy_violations", description: "Finds amount, missing-receipt, and late-submission policy violations.")]
public sealed class PolicyViolationTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public PolicyViolationTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Finds policy violations.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Policy findings.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        var findings = new List<ExpenseEvidence>();
        foreach (var item in ExpenseToolSupport.Filter(data, input))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var rule in data.Policies.Where(value => value.CategoryId == "ALL" || value.CategoryId.Equals(item.CategoryId, StringComparison.OrdinalIgnoreCase)))
            {
                var amount = ExpenseToolSupport.Normalize(data, item);
                if (amount > rule.MaximumReportingAmount) findings.Add(ExpenseToolSupport.Evidence(data, item, rule.RuleId, $"Amount {amount:N2} exceeds {rule.MaximumReportingAmount:N2} {data.ReportingCurrency}."));
                if (rule.ReceiptRequired && !item.HasReceipt) findings.Add(ExpenseToolSupport.Evidence(data, item, rule.RuleId, "A required receipt is missing."));
                var lag = item.SubmittedDate.DayNumber - item.ExpenseDate.DayNumber;
                if (lag > rule.MaximumSubmissionDays) findings.Add(ExpenseToolSupport.Evidence(data, item, rule.RuleId, $"Submission took {lag} days; the limit is {rule.MaximumSubmissionDays}."));
            }
        }
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Detected {findings.Count} policy violations.", findings, findings.Sum(value => value.ReportingAmount)));
    }
}
