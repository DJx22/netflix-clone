using FluentValidation.TestHelper;
using Subscription.Application.DTOs;
using Subscription.Application.Validators;

namespace Subscription.Tests.Application.Validators;

/// <summary>
/// Tests for <see cref="ChangePlanRequestValidator"/>.
/// </summary>
public sealed class ChangePlanRequestValidatorTests
{
    private readonly ChangePlanRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceNewPlanId_FailsWithMessage(string newPlanId)
    {
        var result = _validator.TestValidate(new ChangePlanRequest(newPlanId));
        result.ShouldHaveValidationErrorFor(x => x.NewPlanId)
              .WithErrorMessage("NewPlanId is required.");
    }

    [Fact]
    public void Validate_ValidNewPlanId_Passes()
    {
        var result = _validator.TestValidate(new ChangePlanRequest("premium"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
