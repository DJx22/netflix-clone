using FluentValidation;
using Profile.Application.DTOs;
using Profile.Domain.ValueObjects;

namespace Profile.Application.Validators;

/// <summary>
/// Application-layer validation for <see cref="UpdateProfileRequest"/>.
/// Same defence-in-depth rationale as <see cref="CreateProfileRequestValidator"/>.
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(DisplayName.MaxLength);
    }
}
