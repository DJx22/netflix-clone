using FluentValidation;
using Streaming.Application.DTOs;

namespace Streaming.Application.Validators;

/// <summary>
/// Validates <see cref="UpsertMediaAssetRequest"/> before it reaches application logic.
/// Structural validation only — enforces the OpenAPI schema constraints.
/// </summary>
public sealed class UpsertMediaAssetRequestValidator : AbstractValidator<UpsertMediaAssetRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public UpsertMediaAssetRequestValidator()
    {
        RuleFor(x => x.TitleId)
            .NotEmpty()
            .WithMessage("titleId is required.");

        RuleFor(x => x.MediaUrl)
            .NotEmpty()
            .WithMessage("mediaUrl is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.MediaUrl))
            .WithMessage("mediaUrl must be a valid absolute URI.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("contentType is required.");

        RuleFor(x => x.DurationSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage("durationSeconds must be zero or positive.");
    }
}
