using Catalog.Application.Repositories;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Handles <see cref="ListGenresQuery"/> by returning the distinct genre labels
/// from the read repository.
/// </summary>
public sealed class ListGenresQueryHandler : IRequestHandler<ListGenresQuery, IReadOnlyList<string>>
{
    private readonly ICatalogReadRepository _readRepository;

    public ListGenresQueryHandler(ICatalogReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<IReadOnlyList<string>> Handle(
        ListGenresQuery query,
        CancellationToken cancellationToken)
    {
        return await _readRepository
            .ListDistinctGenresAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
