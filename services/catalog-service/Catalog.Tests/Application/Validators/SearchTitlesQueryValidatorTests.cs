using Catalog.Application.Queries;
using Catalog.Application.Validators;
using FluentValidation.TestHelper;

namespace Catalog.Tests.Application.Validators;

/// <summary>
/// Unit tests for <see cref="SearchTitlesQueryValidator"/>.
/// Covers the pagination bounds from the OpenAPI spec: page ≥ 1, pageSize 1–100.
/// </summary>
public sealed class SearchTitlesQueryValidatorTests
{
    private readonly SearchTitlesQueryValidator _validator = new();

    // ── Page ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_PageZero_HasValidationError()
    {
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, Page: 0, PageSize: 20));
        result.ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Validate_NegativePage_HasValidationError()
    {
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, Page: -1, PageSize: 20));
        result.ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Validate_PageOne_HasNoValidationError()
    {
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, Page: 1, PageSize: 20));
        result.ShouldNotHaveValidationErrorFor(q => q.Page);
    }

    // ── PageSize ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_PageSizeZero_HasValidationError()
    {
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, 1, PageSize: 0));
        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Validate_PageSizeExceedsMax_HasValidationError()
    {
        var result = _validator.TestValidate(
            new SearchTitlesQuery(null, null, 1, PageSize: SearchTitlesQueryValidator.MaxPageSize + 1));
        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Validate_PageSizeAtMax_HasNoValidationError()
    {
        var result = _validator.TestValidate(
            new SearchTitlesQuery(null, null, 1, PageSize: SearchTitlesQueryValidator.MaxPageSize));
        result.ShouldNotHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Validate_PageSizeOne_HasNoValidationError()
    {
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, 1, PageSize: 1));
        result.ShouldNotHaveValidationErrorFor(q => q.PageSize);
    }

    // ── Optional filters — not validated ──────────────────────────────────────

    [Fact]
    public void Validate_NullSearchAndGenre_HasNoValidationError()
    {
        // Null search/genre are explicitly allowed — the spec treats them as "no filter"
        var result = _validator.TestValidate(new SearchTitlesQuery(null, null, 1, 20));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
