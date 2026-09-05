using System.Globalization;
using Runiq.AI.Expense.Data;
using Runiq.AI.Expense.Domain;

namespace Runiq.AI.Expense.Tools;

internal static class ExpenseToolSupport
{
    public static IReadOnlyList<ExpenseTransaction> Filter(
        ExpenseDataSet data,
        ExpenseToolInput input,
        bool includeExpenseId = true) =>
        data.Transactions.Where(item =>
            (!includeExpenseId || string.IsNullOrWhiteSpace(input.ExpenseId) || item.Id.Equals(input.ExpenseId, StringComparison.OrdinalIgnoreCase)) &&
            (ParseDate(input.StartDate) is not { } start || item.ExpenseDate >= start) &&
            (ParseDate(input.EndDate) is not { } end || item.ExpenseDate <= end) &&
            Matches(item.DepartmentId, input.DepartmentId, data.Departments, value => value.Name) &&
            Matches(item.EmployeeId, input.EmployeeId, data.Employees, value => value.Name) &&
            Matches(item.VendorId, input.VendorId, data.Vendors, value => value.Name) &&
            Matches(item.CategoryId, input.CategoryId, data.Categories, value => value.Name) &&
            (string.IsNullOrWhiteSpace(input.OriginalCurrency) || item.Currency.Equals(input.OriginalCurrency, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(input.ApprovalStatus) || item.Status.ToString().Equals(input.ApprovalStatus, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    public static decimal Normalize(ExpenseDataSet data, ExpenseTransaction item)
    {
        var rate = data.Rates.Single(value =>
            value.Currency.Equals(item.Currency, StringComparison.OrdinalIgnoreCase) &&
            item.ExpenseDate >= value.EffectiveFrom &&
            item.ExpenseDate <= value.EffectiveTo);
        return decimal.Round(item.Amount * rate.RateToReporting, 2, MidpointRounding.AwayFromZero);
    }

    public static ExpenseEvidence Evidence(ExpenseDataSet data, ExpenseTransaction item, string rule, string rationale) => new(
        item.Id,
        item.Id,
        rule,
        rationale,
        Normalize(data, item),
        data.ReportingCurrency,
        $"{item.Amount:N2} {item.Currency}",
        $"{item.SourceFile}:{item.SourceRow}");

    public static ExpenseAnalysisResponse Response(
        ExpenseDataSet data,
        ExpenseToolInput input,
        string summary,
        IEnumerable<ExpenseEvidence> evidence,
        decimal total)
    {
        var all = evidence.ToArray();
        var pageNumber = input.PageNumber > 0 ? input.PageNumber : 1;
        var pageSize = input.PageSize > 0 ? Math.Min(input.PageSize, 500) : 100;
        var page = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        return new ExpenseAnalysisResponse(summary, data.ReportingCurrency, total, page, all.Length, page.Length != all.Length, pageNumber, pageSize);
    }

    private static DateOnly? ParseDate(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool Matches<T>(string id, string? requested, IReadOnlyDictionary<string, T> values, Func<T, string> name) =>
        string.IsNullOrWhiteSpace(requested) ||
        id.Equals(requested, StringComparison.OrdinalIgnoreCase) ||
        values.TryGetValue(id, out var value) && name(value).Equals(requested, StringComparison.OrdinalIgnoreCase);
}
