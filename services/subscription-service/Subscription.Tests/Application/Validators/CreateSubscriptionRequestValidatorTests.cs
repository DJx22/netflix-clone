using FluentAssertions;
using FluentValidation.TestHelper;
using Subscription.Application.DTOs;
using Subscription.Application.Validators;

namespace Subscription.Tests.Application.Validators;

/// <summary>
/// Tests for <see cref="CreateSubscriptionRequestValidator"/>.
/// §12: every incoming request DTO is validated before it reaches application logic.
/// §14: one logical assertion focus per test.
/// </summary>
public sealed class CreateSubscriptionRequestValidatorTests
{
    private readonly CreateSubscriptionRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespacePlanId_FailsWithMessage(string planId)
    {
        var result = _validator.TestValidate(new CreateSubscriptionRequest(planId));
        result.ShouldHaveValidationErrorFor(x => x.PlanId)
              .WithErrorMessage("PlanId is required.");
    }

    [Fact]
    public void Validate_ValidPlanId_Passes()
    {
        var result = _validator.TestValidate(new CreateSubscriptionRequest("basic"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
