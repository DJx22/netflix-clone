using FluentValidation;
using Subscription.Application.DTOs;

namespace Subscription.Application.Validators;

/// <summary>
/// Validates <see cref="ChangePlanRequest"/> before it reaches application logic.
/// Structural validation only — plan existence is an application-service concern,
/// not a validator concern (it requires a repository call).
/// </summary>
public sealed class ChangePlanRequestValidator : AbstractValidator<ChangePlanRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public ChangePlanRequestValidator()
    {
        RuleFor(x => x.NewPlanId)
            .NotEmpty()
            .WithMessage("NewPlanId is required.");
    }
}
