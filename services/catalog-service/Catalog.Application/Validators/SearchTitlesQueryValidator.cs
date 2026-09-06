using Catalog.Application.Queries;
using FluentValidation;

namespace Catalog.Application.Validators;

/// <summary>
/// Validates <see cref="SearchTitlesQuery"/> pagination parameters before the
/// handler runs.
/// </summary>
/// <remarks>
/// Bounds match the OpenAPI spec: <c>page</c> ≥ 1, 1 ≤ <c>pageSize</c> ≤ 100.
/// Search term and genre are optional so no validation is applied to them here —
/// Infrastructure handles the case of no filter by returning all titles.
/// </remarks>
public sealed class SearchTitlesQueryValidator : AbstractValidator<SearchTitlesQuery>
{
    /// <summary>Maximum page size accepted by the spec.</summary>
    public const int MaxPageSize = 100;

    public SearchTitlesQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, MaxPageSize);
    }
}
