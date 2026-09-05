namespace Runiq.AI.Expense.Tools;

/// <summary>Describes whether an expense analysis completed within its configured work bound.</summary>
public enum ExpenseAnalysisStatus
{
    /// <summary>The complete analysis and exact totals are available.</summary>
    Completed,

    /// <summary>The near-duplicate candidate budget was exhausted and the caller should narrow the filter.</summary>
    CandidateWorkLimitExceeded
}
