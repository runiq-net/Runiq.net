namespace Runiq.AI.Expense.Domain;

/// <summary>Provides the invariant identifier semantics shared by import, filtering, and analysis.</summary>
internal static class IdentifierSemantics
{
    /// <summary>Returns a trimmed, culture-invariant canonical key while preserving source values elsewhere for display.</summary>
    /// <param name="value">Identifier or grouping value to canonicalize.</param>
    /// <returns>A stable key equivalent to ordinal case-insensitive comparison.</returns>
    internal static string Canonicalize(string value) => value.Trim().ToUpperInvariant();
}
