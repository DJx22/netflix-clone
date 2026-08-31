using FluentValidation.TestHelper;
using Identity.Application.DTOs;
using Identity.Application.Validators;

namespace Identity.Tests.Application.Validators;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_PassesAllRules()
    {
        var result = _validator.TestValidate(new LoginRequest
        {
            Email = "user@example.com",
            Password = "anypassword"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEmail_HasEmailError(string? email)
    {
        var result = _validator.TestValidate(new LoginRequest { Email = email!, Password = "anypassword" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_MalformedEmail_HasEmailError()
    {
        var result = _validator.TestValidate(new LoginRequest { Email = "notvalid", Password = "anypassword" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyPassword_HasPasswordError(string? password)
    {
        // Login validator only checks non-empty — domain rules are re-enforced by Password.From.
        var result = _validator.TestValidate(new LoginRequest { Email = "user@example.com", Password = password! });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
