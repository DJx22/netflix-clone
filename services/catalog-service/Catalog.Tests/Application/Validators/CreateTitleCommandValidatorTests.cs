using Catalog.Application.Commands;
using Catalog.Application.Validators;
using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Catalog.Tests.Application.Validators;

/// <summary>
/// Unit tests for <see cref="CreateTitleCommandValidator"/>.
/// Each test covers exactly one field rule — one validator failure per test (§14).
/// </summary>
public sealed class CreateTitleCommandValidatorTests
{
    private readonly CreateTitleCommandValidator _validator = new();

    private static CreateTitleCommand Valid(
        string? name          = null,
        string[]? genres      = null,
        int? year             = null,
        string? rating        = null,
        int? durationMinutes  = null) =>
        TitleTestData.ValidCreateCommand(name, genres, year, rating, durationMinutes);

    // ── Name ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyName_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(name: ""));
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(name: new string('x', Title.MaxNameLength + 1)));
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_ValidName_HasNoValidationError()
    {
        var result = _validator.TestValidate(Valid(name: "Interstellar"));
        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }

    // ── Genres ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyGenreList_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(genres: []));
        result.ShouldHaveValidationErrorFor(c => c.Genres);
    }

    [Fact]
    public void Validate_GenreItemExceedsMaxLength_HasValidationError()
    {
        var longGenre = new string('x', Genre.MaxLength + 1);
        var result    = _validator.TestValidate(Valid(genres: [longGenre]));
        result.ShouldHaveValidationErrorFor(x => x.Genres);
    }

    // ── ReleaseYear ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReleaseYearBelowMinimum_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(year: Title.MinReleaseYear - 1));
        result.ShouldHaveValidationErrorFor(c => c.ReleaseYear);
    }

    [Fact]
    public void Validate_ReleaseYearInFuture_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(year: DateTime.UtcNow.Year + 1));
        result.ShouldHaveValidationErrorFor(c => c.ReleaseYear);
    }

    [Fact]
    public void Validate_CurrentYear_HasNoValidationError()
    {
        var result = _validator.TestValidate(Valid(year: DateTime.UtcNow.Year));
        result.ShouldNotHaveValidationErrorFor(c => c.ReleaseYear);
    }

    // ── MaturityRating ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyMaturityRating_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(rating: ""));
        result.ShouldHaveValidationErrorFor(c => c.MaturityRating);
    }

    [Fact]
    public void Validate_MaturityRatingExceedsMaxLength_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(rating: new string('x', MaturityRating.MaxLength + 1)));
        result.ShouldHaveValidationErrorFor(c => c.MaturityRating);
    }

    // ── DurationMinutes ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_DurationBelowMinimum_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(durationMinutes: Title.MinDurationMinutes - 1));
        result.ShouldHaveValidationErrorFor(c => c.DurationMinutes);
    }

    [Fact]
    public void Validate_DurationAtMinimum_HasNoValidationError()
    {
        var result = _validator.TestValidate(Valid(durationMinutes: Title.MinDurationMinutes));
        result.ShouldNotHaveValidationErrorFor(c => c.DurationMinutes);
    }

    // ── All valid ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsValid_PassesWithNoErrors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
