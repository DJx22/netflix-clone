using FluentValidation;
using Streaming.Application.DTOs;

namespace Streaming.Application.Validators;

/// <summary>
/// Validates <see cref="SavePositionRequest"/> before it reaches application logic.
/// Structural validation only — enforces the OpenAPI schema constraints:
/// <c>profileId</c> is required and <c>positionSeconds</c> is zero or positive.
/// </summary>
public sealed class SavePositionRequestValidator : AbstractValidator<SavePositionRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public SavePositionRequestValidator()
    {
        RuleFor(x => x.ProfileId)
            .NotEmpty()
            .WithMessage("profileId is required.");

        RuleFor(x => x.PositionSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("positionSeconds must be zero or positive.");
    }
}
