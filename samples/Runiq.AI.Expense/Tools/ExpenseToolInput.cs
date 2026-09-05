namespace Runiq.AI.Expense.Tools;

/// <summary>Defines optional, bounded filters shared by expense analysis tools.</summary>
public sealed record ExpenseToolInput
{
    /// <summary>Gets the exact expense identifier filter.</summary>
    public string? ExpenseId { get; init; }

    /// <summary>Gets the inclusive expense start date in yyyy-MM-dd format.</summary>
    public string? StartDate { get; init; }

    /// <summary>Gets the inclusive expense end date in yyyy-MM-dd format.</summary>
    public string? EndDate { get; init; }

    /// <summary>Gets the department identifier or name filter.</summary>
    public string? DepartmentId { get; init; }

    /// <summary>Gets the employee identifier or name filter.</summary>
    public string? EmployeeId { get; init; }

    /// <summary>Gets the vendor identifier or name filter.</summary>
    public string? VendorId { get; init; }

    /// <summary>Gets the category identifier or name filter.</summary>
    public string? CategoryId { get; init; }

    /// <summary>Gets the transaction's exact original ISO currency filter; this is not the reporting currency.</summary>
    public string? OriginalCurrency { get; init; }

    /// <summary>Gets the approval status filter: Pending, Approved, Rejected, or NeedsInfo.</summary>
    public string? ApprovalStatus { get; init; }

    /// <summary>Gets the one-based result page number.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Gets the requested result page size, or the configured default when omitted.</summary>
    public int PageSize { get; init; } = 100;
}
