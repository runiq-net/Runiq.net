namespace Runiq.AI.Expense.Tools;

/// <summary>Represents a source-backed finding returned to the agent.</summary>
/// <param name="FindingId">Stable finding identifier.</param><param name="ExpenseIds">Comma-delimited source expense identifiers.</param><param name="RuleOrMethod">Rule, threshold, or method used.</param><param name="Rationale">Calculated explanation.</param><param name="ReportingAmount">Amount normalized into reporting currency.</param><param name="ReportingCurrency">Reporting ISO currency.</param><param name="OriginalAmounts">Original amount and currency evidence, when applicable.</param><param name="Sources">Workbook and row evidence, when applicable.</param><param name="SourceRecordCount">Total source records supporting the finding.</param><param name="SourcesTruncated">Whether source identifiers and locations were bounded.</param><param name="ActualAmount">Actual spend for a budget finding.</param><param name="BudgetAmount">Budget for a budget finding.</param><param name="VarianceAmount">Signed actual-minus-budget variance.</param><param name="VarianceDirection">OverBudget or UnderBudget direction.</param><param name="DirectionRank">One-based variance rank within its direction.</param><param name="TargetVendorId">Preferred target vendor identifier.</param><param name="TargetVendorName">Preferred target vendor name.</param><param name="ConsolidationGroup">Comparable vendor group.</param><param name="CurrentSpend">Eligible current spend.</param><param name="SavingsRate">Explicit estimated savings rate.</param><param name="EstimatedSavings">Estimated savings in reporting currency.</param><param name="Assumption">Explicit estimate assumption.</param>
public sealed record ExpenseEvidence(
    string FindingId,
    string ExpenseIds,
    string RuleOrMethod,
    string Rationale,
    decimal ReportingAmount,
    string ReportingCurrency,
    string? OriginalAmounts,
    string? Sources,
    int SourceRecordCount = 1,
    bool SourcesTruncated = false,
    decimal? ActualAmount = null,
    decimal? BudgetAmount = null,
    decimal? VarianceAmount = null,
    string? VarianceDirection = null,
    int? DirectionRank = null,
    string? TargetVendorId = null,
    string? TargetVendorName = null,
    string? ConsolidationGroup = null,
    decimal? CurrentSpend = null,
    decimal? SavingsRate = null,
    decimal? EstimatedSavings = null,
    string? Assumption = null);
