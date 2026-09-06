using Catalog.Application.DTOs;
using Catalog.Application.Repositories;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>
/// Handles <see cref="GetTitleQuery"/> by projecting a single title document from
/// MongoDB into a <see cref="TitleDetailDto"/>.
/// </summary>
/// <remarks>
/// The read repository returns a DTO directly — no <c>Title</c> aggregate is ever
/// constructed on this path (ADR 0005).  The handler returns <c>null</c> and lets
/// the API layer produce the 404 response; a thrown exception would bypass the
/// content-negotiation logic in the controller.
/// </remarks>
public sealed class GetTitleQueryHandler : IRequestHandler<GetTitleQuery, TitleDetailDto?>
{
    private readonly ICatalogReadRepository _readRepository;

    public GetTitleQueryHandler(ICatalogReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<TitleDetailDto?> Handle(
        GetTitleQuery query,
        CancellationToken cancellationToken)
    {
        return await _readRepository
            .FindDetailByIdAsync(query.TitleId, cancellationToken)
            .ConfigureAwait(false);
    }
}
