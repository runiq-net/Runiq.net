namespace Runiq.AI.Expense.Tools;

/// <summary>Represents an auditable deterministic analysis result.</summary>
/// <param name="Summary">Human-readable result summary.</param><param name="ReportingCurrency">Reporting ISO currency.</param><param name="TotalReportingAmount">Normalized total across all evaluated matches.</param><param name="Evidence">Bounded source-backed findings for the requested page.</param><param name="TotalFindingCount">Total findings before paging, or the evaluated lower bound when the count is not exact.</param><param name="IsTruncated">Whether findings exist outside the returned page or analysis stopped at its work limit.</param><param name="PageNumber">One-based returned page number.</param><param name="PageSize">Validated page size.</param><param name="TotalCountIsExact">Whether total count and amount cover the complete analysis.</param><param name="AnalysisStatus">Typed completion status for the analysis.</param>
public sealed record ExpenseAnalysisResponse(
    string Summary,
    string ReportingCurrency,
    decimal TotalReportingAmount,
    IReadOnlyList<ExpenseEvidence> Evidence,
    long TotalFindingCount,
    bool IsTruncated,
    int PageNumber,
    int PageSize,
    bool TotalCountIsExact = true,
    ExpenseAnalysisStatus AnalysisStatus = ExpenseAnalysisStatus.Completed);
