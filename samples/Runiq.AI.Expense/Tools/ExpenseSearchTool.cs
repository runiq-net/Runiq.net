using Runiq.AI.Agents.Tools;
using Runiq.AI.Expense.Data;

namespace Runiq.AI.Expense.Tools;

/// <summary>Searches filtered expense records.</summary>
[RuniqTool(name: "expense_search", description: "Searches expenses by exact expense ID, ISO date, department, category, vendor, employee, original transaction currency, and approval status.")]
public sealed class ExpenseSearchTool : IRuniqTool<ExpenseToolInput, ExpenseAnalysisResponse>
{
    private readonly ExpenseDataSet data;

    /// <summary>Initializes the tool.</summary><param name="data">Imported expense data.</param>
    public ExpenseSearchTool(ExpenseDataSet data) => this.data = data;

    /// <summary>Searches expenses.</summary><param name="input">Search filters.</param><param name="cancellationToken">Cancellation signal.</param><returns>Matching expenses.</returns>
    public Task<ExpenseAnalysisResponse> ExecuteAsync(ExpenseToolInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = ExpenseToolSupport.Filter(data, input).OrderBy(item => item.ExpenseDate).ThenBy(item => item.Id).ToArray();
        var evidence = rows.Select(item => ExpenseToolSupport.Evidence(data, item, "SOURCE-TRANSACTION", $"{data.Employees[item.EmployeeId].Name}; {data.Departments[item.DepartmentId].Name}; {data.Vendors[item.VendorId].Name}; {data.Categories[item.CategoryId].Name}; {item.Status}; receipt={item.HasReceipt}."));
        return Task.FromResult(ExpenseToolSupport.Response(data, input, $"Found {rows.Length} matching expenses.", evidence, rows.Sum(item => ExpenseToolSupport.Normalize(data, item))));
    }
}
