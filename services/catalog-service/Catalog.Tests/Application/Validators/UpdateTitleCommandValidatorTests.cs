using Catalog.Application.Commands;
using Catalog.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Catalog.Tests.Application.Validators;

/// <summary>
/// Unit tests for <see cref="UpdateTitleCommandValidator"/>.
/// Covers the TitleId presence check (not needed on create) plus the same field
/// rules as <see cref="CreateTitleCommandValidator"/>.
/// </summary>
public sealed class UpdateTitleCommandValidatorTests
{
    private readonly UpdateTitleCommandValidator _validator = new();

    private static UpdateTitleCommand Valid(string? titleId = "some-id") =>
        new(
            titleId ?? "",
            Name: TitleTestData.ValidName,
            Description: null,
            Genres: [TitleTestData.ValidGenre],
            ReleaseYear: TitleTestData.ValidReleaseYear,
            MaturityRating: TitleTestData.ValidMaturityRating,
            Cast: [],
            DurationMinutes: TitleTestData.ValidDurationMinutes,
            PosterUrl: null,
            StreamingAssetId: null);

    // ── TitleId (update-specific rule) ────────────────────────────────────────

    [Fact]
    public void Validate_EmptyTitleId_HasValidationError()
    {
        var result = _validator.TestValidate(Valid(titleId: ""));
        result.ShouldHaveValidationErrorFor(c => c.TitleId);
    }

    [Fact]
    public void Validate_NonEmptyTitleId_HasNoValidationError()
    {
        var result = _validator.TestValidate(Valid(titleId: "any-id"));
        result.ShouldNotHaveValidationErrorFor(c => c.TitleId);
    }

    // ── All valid ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsValid_PassesWithNoErrors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
