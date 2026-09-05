using System.ComponentModel.DataAnnotations;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Represents a chat request sent to an agent through the hosted API.
/// </summary>
/// <param name="Message">The user message to send to the agent.</param>
/// <param name="ResponseMode">The requested response delivery mode.</param>
/// <param name="IndexName">An optional RAG index override.</param>
public sealed record AgentChatRequest(
    [Required]
    [StringLength(16_000, MinimumLength = 1)]
    string Message,
    [EnumDataType(typeof(AgentChatResponseMode))]
    AgentChatResponseMode ResponseMode = AgentChatResponseMode.Stream,
    [StringLength(128)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    string? IndexName = null) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (string.IsNullOrEmpty(Message) || Message.Length > 16_000)
        {
            yield return new ValidationResult(
                "Message must contain between 1 and 16000 characters.",
                [nameof(Message)]);
        }

        if (!Enum.IsDefined(ResponseMode))
        {
            yield return new ValidationResult(
                "ResponseMode must be a defined value.",
                [nameof(ResponseMode)]);
        }

        if (IndexName is { Length: > 0 } &&
            (IndexName.Length > 128 || !IsValidIndexName(IndexName)))
        {
            yield return new ValidationResult(
                "IndexName must be a valid index identifier with at most 128 characters.",
                [nameof(IndexName)]);
        }
    }

    private static bool IsValidIndexName(string value) =>
        char.IsAsciiLetterOrDigit(value[0]) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
