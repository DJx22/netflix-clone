using FluentValidation.TestHelper;
using Identity.Application.DTOs;
using Identity.Application.Validators;

namespace Identity.Tests.Application.Validators;

public sealed class RefreshTokenRequestValidatorTests
{
    private readonly RefreshTokenRequestValidator _validator = new();

    [Fact]
    public void Validate_NonEmptyToken_Passes()
    {
        var result = _validator.TestValidate(new RefreshTokenRequest { RefreshToken = "any-opaque-token" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyToken_HasRefreshTokenError(string? token)
    {
        var result = _validator.TestValidate(new RefreshTokenRequest { RefreshToken = token! });
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
