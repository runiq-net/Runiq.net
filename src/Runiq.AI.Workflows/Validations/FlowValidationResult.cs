#pragma warning disable CS1591 // Legacy documentation debt is isolated to this existing API file.

namespace Runiq.AI.Workflows.Validations;

/// <summary>
/// Captures structural validation errors for a flow definition.
/// </summary>
public sealed class FlowValidationResult
{
    private FlowValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public bool IsValid { get; }

    public IReadOnlyList<string> Errors { get; }

    public static FlowValidationResult Success()
    {
        return new FlowValidationResult(true, []);
    }

    public static FlowValidationResult Failure(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new FlowValidationResult(false, errors);
    }
}

