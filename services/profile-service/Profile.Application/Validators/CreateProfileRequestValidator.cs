using FluentValidation;
using Profile.Application.DTOs;
using Profile.Domain.ValueObjects;

namespace Profile.Application.Validators;

/// <summary>
/// Application-layer validation for <see cref="CreateProfileRequest"/>.
/// Domain rules (max length, non-empty) are re-expressed here as FluentValidation
/// rules so the API returns a structured 400 with field-level error messages
/// before the domain value object is ever constructed. The domain still enforces
/// the same invariants — this is a defence-in-depth layer, not the single source of truth.
/// </summary>
public sealed class CreateProfileRequestValidator : AbstractValidator<CreateProfileRequest>
{
    public CreateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(DisplayName.MaxLength);
    }
}
