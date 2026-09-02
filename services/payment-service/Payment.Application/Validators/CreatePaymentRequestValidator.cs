using FluentValidation;
using Payment.Application.DTOs;

namespace Payment.Application.Validators;

/// <summary>
/// Validates <see cref="CreatePaymentRequest"/> before it reaches application logic.
/// Structural validation only — subscription existence is an application-service concern,
/// not a validator concern (it requires a repository call).
/// </summary>
public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.SubscriptionId)
            .NotEmpty()
            .WithMessage("SubscriptionId is required.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be zero or greater.")
            .Must(a => decimal.Round(a, 2) == a)
            .WithMessage("Amount must not have more than 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be a 3-character ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must contain only letters.");
    }
}
