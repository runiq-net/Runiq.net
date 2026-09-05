using Runiq.AI.Expense.Domain;

namespace Runiq.AI.Expense.Data;

/// <summary>Contains validated reference and transaction data loaded from the sample workbooks.</summary>
public sealed class ExpenseDataSet
{
    /// <summary>Initializes a validated expense data set.</summary>
    /// <param name="reportingCurrency">Reporting ISO currency.</param><param name="departments">Departments by identifier.</param><param name="employees">Employees by identifier.</param><param name="vendors">Vendors by identifier.</param><param name="categories">Categories by identifier.</param><param name="rates">Exchange rates.</param><param name="budgets">Monthly budgets.</param><param name="policies">Policy rules.</param><param name="transactions">Expense transactions.</param>
    internal ExpenseDataSet(string reportingCurrency, IReadOnlyDictionary<string, Department> departments, IReadOnlyDictionary<string, Employee> employees, IReadOnlyDictionary<string, Vendor> vendors, IReadOnlyDictionary<string, ExpenseCategory> categories, IReadOnlyList<ExchangeRate> rates, IReadOnlyList<Budget> budgets, IReadOnlyList<PolicyRule> policies, IReadOnlyList<ExpenseTransaction> transactions)
    {
        ReportingCurrency = reportingCurrency;
        Departments = departments;
        Employees = employees;
        Vendors = vendors;
        Categories = categories;
        Rates = rates;
        Budgets = budgets;
        Policies = policies;
        Transactions = transactions;
    }

    /// <summary>Gets the reporting ISO currency.</summary>
    public string ReportingCurrency { get; }
    /// <summary>Gets departments keyed by identifier.</summary>
    internal IReadOnlyDictionary<string, Department> Departments { get; }
    /// <summary>Gets employees keyed by identifier.</summary>
    internal IReadOnlyDictionary<string, Employee> Employees { get; }
    /// <summary>Gets vendors keyed by identifier.</summary>
    internal IReadOnlyDictionary<string, Vendor> Vendors { get; }
    /// <summary>Gets categories keyed by identifier.</summary>
    internal IReadOnlyDictionary<string, ExpenseCategory> Categories { get; }
    /// <summary>Gets effective-dated exchange rates.</summary>
    internal IReadOnlyList<ExchangeRate> Rates { get; }
    /// <summary>Gets monthly department budgets.</summary>
    internal IReadOnlyList<Budget> Budgets { get; }
    /// <summary>Gets policy rules.</summary>
    internal IReadOnlyList<PolicyRule> Policies { get; }
    /// <summary>Gets imported transactions.</summary>
    internal IReadOnlyList<ExpenseTransaction> Transactions { get; }

    /// <summary>Converts an original amount by using the workbook rate effective on the expense date.</summary>
    /// <param name="amount">Original amount.</param><param name="currency">Original ISO currency.</param><param name="date">Expense date.</param>
    /// <returns>The amount in reporting currency.</returns>
    /// <exception cref="InvalidDataException">Thrown when no valid rate exists.</exception>
    public decimal Convert(decimal amount, string currency, DateOnly date)
    {
        var rate = Rates.SingleOrDefault(item => item.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase) && date >= item.EffectiveFrom && date <= item.EffectiveTo)
            ?? throw new InvalidDataException($"No exchange rate exists for currency '{currency}' on {date:yyyy-MM-dd}.");
        return decimal.Round(amount * rate.RateToReporting, 2, MidpointRounding.AwayFromZero);
    }
}
