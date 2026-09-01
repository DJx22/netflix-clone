using FluentAssertions;
using FluentValidation.TestHelper;
using Profile.Application.DTOs;
using Profile.Application.Validators;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Application.Validators;

/// <summary>
/// Every FluentValidation rule on CreateProfileRequestValidator gets at least one
/// passing and one failing test. Rules mirror the domain invariants but produce
/// field-level 400 errors before the domain VO is ever constructed.
/// </summary>
public sealed class CreateProfileRequestValidatorTests
{
    private readonly CreateProfileRequestValidator _validator = new();

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
        var request = new CreateProfileRequest { DisplayName = displayName! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_DisplayNameExceedsMaxLength_HasDisplayNameError()
    {
        // openapi.yaml: maxLength 40.
        var tooLong = new string('A', DisplayName.MaxLength + 1);
        var request = new CreateProfileRequest { DisplayName = tooLong };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_DisplayNameExactlyMaxLength_Passes()
    {
        // Boundary: exactly 40 characters must pass.
        var exactly40 = new string('A', DisplayName.MaxLength);
        var request = new CreateProfileRequest { DisplayName = exactly40 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CreateProfileRequest ValidRequest() => new()
    {
        DisplayName = "Alice"
    };
}
