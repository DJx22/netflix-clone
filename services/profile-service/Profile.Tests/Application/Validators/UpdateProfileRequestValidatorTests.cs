using FluentAssertions;
using FluentValidation.TestHelper;
using Profile.Application.DTOs;
using Profile.Application.Validators;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Application.Validators;

/// <summary>
/// Every FluentValidation rule on UpdateProfileRequestValidator gets at least one
/// passing and one failing test.
/// </summary>
public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    // ── Valid request (baseline) ──────────────────────────────────────────────

    [Fact]
    public void Validate_ValidRequest_PassesAllRules()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── DisplayName ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyDisplayName_HasDisplayNameError(string? displayName)
    {
        var request = new UpdateProfileRequest { DisplayName = displayName! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_DisplayNameExceedsMaxLength_HasDisplayNameError()
    {
        var tooLong = new string('A', DisplayName.MaxLength + 1);
        var request = new UpdateProfileRequest { DisplayName = tooLong };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_DisplayNameExactlyMaxLength_Passes()
    {
        var exactly40 = new string('A', DisplayName.MaxLength);
        var request = new UpdateProfileRequest { DisplayName = exactly40 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UpdateProfileRequest ValidRequest() => new()
    {
        DisplayName = "Alice"
    };
}
