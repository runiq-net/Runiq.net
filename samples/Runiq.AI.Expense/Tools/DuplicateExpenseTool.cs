using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Finds exact and near duplicate expenses.</summary>
[RuniqTool(name: "duplicate_expenses", description: "Finds exact duplicates and near duplicates within three days and two percent.")]
public sealed class DuplicateExpenseTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public DuplicateExpenseTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Finds duplicate expenses.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Duplicate findings.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        var rows = ExpenseToolSupport.Filter(data, input, includeExpenseId: false);
        var findings = new List<ExpenseEvidence>();
        for (var leftIndex = 0; leftIndex < rows.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < rows.Count; rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var left = rows[leftIndex];
                var right = rows[rightIndex];
                if (!string.IsNullOrWhiteSpace(input.ExpenseId) &&
                    !left.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase) &&
                    !right.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!left.EmployeeId.Equals(right.EmployeeId, StringComparison.OrdinalIgnoreCase) || !left.VendorId.Equals(right.VendorId, StringComparison.OrdinalIgnoreCase)) continue;
                var exact = left.InvoiceReference == right.InvoiceReference && left.Amount == right.Amount && left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase);
                var leftAmount = ExpenseToolSupport.Normalize(data, left);
                var rightAmount = ExpenseToolSupport.Normalize(data, right);
                var near = Math.Abs(left.ExpenseDate.DayNumber - right.ExpenseDate.DayNumber) <= 3 && Math.Abs(leftAmount - rightAmount) <= Math.Max(leftAmount, rightAmount) * 0.02m;
                if (!exact && !near) continue;
                findings.Add(new ExpenseEvidence($"{left.Id}:{right.Id}", $"{left.Id},{right.Id}", exact ? "EXACT-DUPLICATE" : "NEAR-DUPLICATE", exact ? "Same employee, vendor, invoice, amount, and currency." : "Same employee and vendor; dates are within three days and reporting amounts within 2%.", leftAmount + rightAmount, data.ReportingCurrency, $"{left.Amount:N2} {left.Currency}; {right.Amount:N2} {right.Currency}", $"{left.SourceFile}:{left.SourceRow}; {right.SourceFile}:{right.SourceRow}", 2));
            }
        }
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Found {findings.Count} duplicate candidates.", findings, findings.Sum(value => value.ReportingAmount)));
    }
}
