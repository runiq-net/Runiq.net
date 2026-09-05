namespace Runiq.AI.Expense.Domain;

/// <summary>Identifies an approval state supported by the sample data.</summary>
public enum ApprovalStatus
{
    /// <summary>The expense is awaiting review.</summary>
    Pending,
    /// <summary>The expense was approved.</summary>
    Approved,
    /// <summary>The expense was rejected.</summary>
    Rejected,
    /// <summary>The expense requires additional information.</summary>
    NeedsInfo
}

/// <summary>Represents a department reference row.</summary>
/// <param name="Id">Stable department identifier.</param><param name="Name">Display name.</param>
internal sealed record Department(string Id, string Name);

/// <summary>Represents an employee reference row.</summary>
/// <param name="Id">Stable employee identifier.</param><param name="Name">Employee name.</param><param name="DepartmentId">Owning department identifier.</param><param name="Title">Job title.</param>
internal sealed record Employee(string Id, string Name, string DepartmentId, string Title);

/// <summary>Represents a vendor reference row.</summary>
/// <param name="Id">Stable vendor identifier.</param><param name="Name">Vendor name.</param><param name="Preferred">Whether procurement prefers the vendor.</param><param name="ConsolidationGroup">Comparable vendor group.</param>
internal sealed record Vendor(string Id, string Name, bool Preferred, string ConsolidationGroup);

/// <summary>Represents an expense category reference row.</summary>
/// <param name="Id">Stable category identifier.</param><param name="Name">Category name.</param>
internal sealed record ExpenseCategory(string Id, string Name);

/// <summary>Represents a workbook exchange rate into the reporting currency.</summary>
/// <param name="Currency">Source ISO currency.</param><param name="RateToReporting">Multiplier into reporting currency.</param><param name="EffectiveFrom">Inclusive effective date.</param><param name="EffectiveTo">Inclusive expiry date.</param>
internal sealed record ExchangeRate(string Currency, decimal RateToReporting, DateOnly EffectiveFrom, DateOnly EffectiveTo);

/// <summary>Represents a monthly department budget in reporting currency.</summary>
/// <param name="DepartmentId">Department identifier.</param><param name="Period">First day of the budget month.</param><param name="Amount">Budget amount.</param>
internal sealed record Budget(string DepartmentId, DateOnly Period, decimal Amount);

/// <summary>Represents an auditable policy threshold.</summary>
/// <param name="RuleId">Stable rule identifier.</param><param name="CategoryId">Category to which the rule applies, or ALL.</param><param name="MaximumReportingAmount">Maximum allowed normalized amount.</param><param name="ReceiptRequired">Whether a receipt is mandatory.</param><param name="MaximumSubmissionDays">Maximum days between expense and submission.</param>
internal sealed record PolicyRule(string RuleId, string CategoryId, decimal MaximumReportingAmount, bool ReceiptRequired, int MaximumSubmissionDays);

/// <summary>Represents an imported expense transaction and its source location.</summary>
/// <param name="Id">Stable transaction identifier.</param><param name="InvoiceReference">Invoice or merchant reference.</param><param name="ExpenseDate">Date incurred.</param><param name="SubmittedDate">Date submitted.</param><param name="EmployeeId">Employee identifier.</param><param name="DepartmentId">Department identifier.</param><param name="VendorId">Vendor identifier.</param><param name="CategoryId">Category identifier.</param><param name="Amount">Original amount.</param><param name="Currency">Original currency.</param><param name="Status">Approval status.</param><param name="HasReceipt">Whether a receipt exists.</param><param name="Description">Business description.</param><param name="SourceFile">Source workbook file name.</param><param name="SourceRow">Source worksheet row.</param>
internal sealed record ExpenseTransaction(string Id, string InvoiceReference, DateOnly ExpenseDate, DateOnly SubmittedDate, string EmployeeId, string DepartmentId, string VendorId, string CategoryId, decimal Amount, string Currency, ApprovalStatus Status, bool HasReceipt, string Description, string SourceFile, int SourceRow);
