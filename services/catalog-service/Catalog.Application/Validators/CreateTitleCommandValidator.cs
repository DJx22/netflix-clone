using Catalog.Application.Commands;
using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using FluentValidation;

namespace Catalog.Application.Validators;

/// <summary>
/// Validates <see cref="CreateTitleCommand"/> before the handler runs.
/// </summary>
/// <remarks>
/// Rules mirror the domain invariants in <see cref="Title"/> so the pipeline
/// returns a structured 400 with field-level errors rather than an unhandled
/// <c>TitleValidationException</c> from the aggregate constructor.  Both layers
/// check the same rules — the validator is the early-exit path, the aggregate is
/// the hard guarantee.
/// </remarks>
public sealed class CreateTitleCommandValidator : AbstractValidator<CreateTitleCommand>
{
    public CreateTitleCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(Title.MaxNameLength);

        RuleFor(c => c.Genres)
            .NotEmpty()
            .WithMessage("At least one genre is required.");

        RuleForEach(c => c.Genres)
            .NotEmpty()
            .MaximumLength(Genre.MaxLength);

        RuleFor(c => c.ReleaseYear)
            .InclusiveBetween(Title.MinReleaseYear, DateTime.UtcNow.Year);

        RuleFor(c => c.MaturityRating)
            .NotEmpty()
            .MaximumLength(MaturityRating.MaxLength);

        RuleFor(c => c.DurationMinutes)
            .GreaterThanOrEqualTo(Title.MinDurationMinutes);
    }
}
