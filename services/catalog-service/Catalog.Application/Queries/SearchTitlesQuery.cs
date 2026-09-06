using Catalog.Application.DTOs;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Returns a paged list of title summaries, optionally filtered by a search term
/// and/or genre.  Maps to <c>GET /api/v1/titles</c>.
/// </summary>
/// <remarks>
/// Pagination bounds (<paramref name="Page"/> ≥ 1, 1 ≤ <paramref name="PageSize"/> ≤ 100)
/// are validated by <c>SearchTitlesQueryValidator</c> before the handler is reached.
/// </remarks>
public sealed record SearchTitlesQuery(
    string? Search,
    string? Genre,
    int Page,
    int PageSize) : IRequest<PagedResultDto<TitleSummaryDto>>;
