namespace Runiq.AI.Expense.Configuration;

/// <summary>Defines validated import and response limits for the expense sample.</summary>
public sealed class ExpenseDataOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "ExpenseData";

    /// <summary>Gets or sets the directory containing the master and transaction workbooks.</summary>
    public string Directory { get; set; } = "SampleData";

    /// <summary>Gets or sets the ISO currency code used for normalized reporting.</summary>
    public string ReportingCurrency { get; set; } = "TRY";

    /// <summary>Gets or sets the maximum number of workbooks accepted during one import.</summary>
    public int MaxWorkbookCount { get; set; } = 10;

    /// <summary>Gets or sets the maximum number of data rows accepted on one worksheet.</summary>
    public int MaxRowsPerSheet { get; set; } = 10_000;

    /// <summary>Gets or sets the default number of findings returned by a tool call.</summary>
    public int DefaultToolResultLimit { get; set; } = 100;

    /// <summary>Gets or sets the maximum number of findings that a caller may request.</summary>
    public int MaxToolResultLimit { get; set; } = 500;

    /// <summary>Gets or sets the shared work-unit limit for near-duplicate preparation, active-index maintenance, range probes, and candidate comparisons.</summary>
    public int MaxDuplicateCandidateComparisons { get; set; } = 250_000;
}
