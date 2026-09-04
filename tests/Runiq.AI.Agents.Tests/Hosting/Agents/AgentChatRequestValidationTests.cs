using System.ComponentModel.DataAnnotations;
using Runiq.AI.Core.Agents;

namespace Runiq.AI.Core.Tests.Agents;

/// <summary>
/// Verifies the Agent Chat request validation contract applied by API controllers.
/// </summary>
public sealed class AgentChatRequestValidationTests
{
    // Undefined numeric response modes must fail model validation before reaching the chat handler.
    [Fact]
    public void Validate_WhenResponseModeIsUndefined_ReturnsValidationError()
    {
        var request = new AgentChatRequest("Hello", (AgentChatResponseMode)99);

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AgentChatRequest.ResponseMode)));
    }

    // Messages beyond the public request limit must fail validation before retrieval or model I/O.
    [Fact]
    public void Validate_WhenMessageExceedsMaximumLength_ReturnsValidationError()
    {
        var request = new AgentChatRequest(new string('a', 16_001), AgentChatResponseMode.Result);

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AgentChatRequest.Message)));
    }

    // Index overrides with unsafe characters must fail validation at the HTTP request boundary.
    [Fact]
    public void Validate_WhenIndexNameHasInvalidFormat_ReturnsValidationError()
    {
        var request = new AgentChatRequest("Hello", AgentChatResponseMode.Result, "../../private");

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AgentChatRequest.IndexName)));
    }

    private static IReadOnlyList<ValidationResult> Validate(AgentChatRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
