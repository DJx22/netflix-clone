using Catalog.Application.Commands;
using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using FluentValidation;

namespace Catalog.Application.Validators;

/// <summary>
/// Validates <see cref="UpdateTitleCommand"/> before the handler runs.
/// </summary>
/// <remarks>
/// Same field rules as <see cref="CreateTitleCommandValidator"/>, plus a non-empty
/// check on <c>TitleId</c> — on the create path the ID is generated internally
/// and never validated here.
/// </remarks>
public sealed class UpdateTitleCommandValidator : AbstractValidator<UpdateTitleCommand>
{
    public UpdateTitleCommandValidator()
    {
        RuleFor(c => c.TitleId)
            .NotEmpty();

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
