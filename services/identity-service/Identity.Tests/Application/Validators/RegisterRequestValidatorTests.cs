using FluentAssertions;
using FluentValidation.TestHelper;
using Identity.Application.DTOs;
using Identity.Application.Validators;
using Identity.Domain.ValueObjects;

namespace Identity.Tests.Application.Validators;

/// <summary>
/// Every FluentValidation rule on RegisterRequestValidator gets at least one
/// passing and one failing test. Rules mirror the domain invariants but produce
/// field-level 400 errors before the domain VO is ever constructed.
/// </summary>
public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    // ── Valid request (baseline) ──────────────────────────────────────────────

    [Fact]
    public void Validate_ValidRequest_PassesAllRules()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Email ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_HasEmailError(string? email)
    {
        var request = new RegisterRequest { Email = email!, Password = "password123", ConfirmPassword = "password123" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_MalformedEmail_HasEmailError()
    {
        var request = new RegisterRequest { Email = "notanemail", Password = "password123", ConfirmPassword = "password123" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmailExceedingMaxLength_HasEmailError()
    {
        var tooLong = new string('a', 244) + "@domain.com";
        tooLong.Length.Should().BeGreaterThan(Email.MaxLength);

        var request = new RegisterRequest { Email = tooLong, Password = "password123", ConfirmPassword = "password123" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Password ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyPassword_HasPasswordError(string? password)
    {
        var request = new RegisterRequest { Email = "user@example.com", Password = password!, ConfirmPassword = password! };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordShorterThanMinLength_HasPasswordError()
    {
        // openapi.yaml line 215: minLength 10.
        var request = new RegisterRequest { Email = "user@example.com", Password = "short1", ConfirmPassword = "short1" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordWithNoDigit_HasPasswordError()
    {
        // openapi.yaml line 216: at least one number.
        var request = new RegisterRequest { Email = "user@example.com", Password = "nodigitshere", ConfirmPassword = "nodigitshere" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must contain at least one number (0-9).");
    }

    [Fact]
    public void Validate_PasswordExactlyMinLength_WithDigit_PassesPasswordRule()
    {
        // Boundary: exactly 10 characters, one digit — must pass.
        var request = new RegisterRequest { Email = "user@example.com", Password = "aaaaaaaaa1", ConfirmPassword = "aaaaaaaaa1" };
        var result  = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // ── ConfirmPassword ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_ConfirmPasswordMismatch_HasConfirmPasswordError()
    {
        // ConfirmPassword matching is an application-layer rule (Password.cs comment).
        var request = new RegisterRequest { Email = "user@example.com", Password = "password123", ConfirmPassword = "different1234" };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
              .WithErrorMessage("Password and confirmation password do not match.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyConfirmPassword_HasConfirmPasswordError(string? confirmPassword)
    {
        var request = new RegisterRequest { Email = "user@example.com", Password = "password123", ConfirmPassword = confirmPassword! };
        var result  = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RegisterRequest ValidRequest() => new()
    {
        Email           = "user@example.com",
        Password        = "securepass1",
        ConfirmPassword = "securepass1"
    };
}
