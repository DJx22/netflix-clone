using FluentValidation;
using Subscription.Application.DTOs;

namespace Subscription.Application.Validators;

/// <summary>
/// Validates <see cref="CreateSubscriptionRequest"/> before it reaches application logic.
/// Structural validation only — plan existence is an application-service concern,
/// not a validator concern (it requires a repository call).
/// </summary>
public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty()
            .WithMessage("PlanId is required.");
    }
}
