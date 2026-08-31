using FluentValidation;
using Identity.Application.DTOs;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Validators;

/// <summary>
/// Application-layer validation for <see cref="RegisterRequest"/>.
/// Domain rules (min length, digit requirement) are re-expressed here as FluentValidation
/// rules so that the API returns a structured 400 with field-level error messages
/// before the domain value object is ever constructed. The domain still enforces the
/// same invariants — this is a defence-in-depth layer, not the single source of truth.
///
/// ConfirmPassword match is a request-level concern (noted as such in Password.cs)
/// and belongs here, not in the domain.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(Password.MinLength)
            .Matches(@"\d").WithMessage("Password must contain at least one number (0-9).");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.Password).WithMessage("Password and confirmation password do not match.");
    }
}
