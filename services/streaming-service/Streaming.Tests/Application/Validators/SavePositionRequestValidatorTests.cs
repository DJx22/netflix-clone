using FluentAssertions;
using FluentValidation.TestHelper;
using Streaming.Application.DTOs;
using Streaming.Application.Validators;

namespace Streaming.Tests.Application.Validators;

/// <summary>
/// Tests for <see cref="SavePositionRequestValidator"/>.
/// §12: every incoming request DTO is validated before it reaches application logic.
/// §14: one logical assertion focus per test.
/// </summary>
public sealed class SavePositionRequestValidatorTests
{
    private readonly SavePositionRequestValidator _validator = new();

    [Fact]
    public void Validate_NegativePositionSeconds_FailsValidation()
    {
        var result = _validator.TestValidate(
            new SavePositionRequest(Guid.NewGuid(), PositionSeconds: -1));

        result.ShouldHaveValidationErrorFor(x => x.PositionSeconds)
              .WithErrorMessage("positionSeconds must be zero or positive.");
    }

    [Fact]
    public void Validate_ZeroPositionSeconds_Passes()
    {
        // Zero is a valid position — it means "start of content / not yet started".
        var result = _validator.TestValidate(
            new SavePositionRequest(Guid.NewGuid(), PositionSeconds: 0));

        result.ShouldNotHaveValidationErrorFor(x => x.PositionSeconds);
    }

    [Fact]
    public void Validate_EmptyProfileId_FailsValidation()
    {
        var result = _validator.TestValidate(
            new SavePositionRequest(ProfileId: Guid.Empty, PositionSeconds: 60));

        result.ShouldHaveValidationErrorFor(x => x.ProfileId)
              .WithErrorMessage("profileId is required.");
    }

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _validator.TestValidate(
            new SavePositionRequest(Guid.NewGuid(), PositionSeconds: 120));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
