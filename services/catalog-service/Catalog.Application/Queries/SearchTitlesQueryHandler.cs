using Catalog.Application.DTOs;
using Catalog.Application.Repositories;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Handles <see cref="SearchTitlesQuery"/> by delegating to the read repository,
/// which projects MongoDB documents directly to <see cref="TitleSummaryDto"/> without
/// loading full <c>Title</c> aggregates.
/// </summary>
public sealed class SearchTitlesQueryHandler
    : IRequestHandler<SearchTitlesQuery, PagedResultDto<TitleSummaryDto>>
{
    private readonly ICatalogReadRepository _readRepository;

    public SearchTitlesQueryHandler(ICatalogReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<PagedResultDto<TitleSummaryDto>> Handle(
        SearchTitlesQuery query,
        CancellationToken cancellationToken)
    {
        return await _readRepository
            .SearchAsync(query.Search, query.Genre, query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);
    }
}
