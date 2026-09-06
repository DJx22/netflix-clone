using Catalog.Application.Repositories;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Handles <see cref="DeleteTitleCommand"/> by verifying the title exists, then
/// delegating removal to the write repository.
/// </summary>
/// <remarks>
/// The existence check before delete is deliberate: the spec returns 404 when
/// no title with that ID exists.  Without the check, Infrastructure would silently
/// succeed (MongoDB's <c>DeleteOne</c> with zero matched documents is not an error),
/// and the controller would return 204 for a title that was never there.
/// </remarks>
public sealed class DeleteTitleCommandHandler : IRequestHandler<DeleteTitleCommand, Unit>
{
    private readonly ICatalogWriteRepository _writeRepository;

    public DeleteTitleCommandHandler(ICatalogWriteRepository writeRepository)
    {
        _writeRepository = writeRepository;
    }

    public async Task<Unit> Handle(
        DeleteTitleCommand command,
        CancellationToken cancellationToken)
    {
        var titleId = new TitleId(command.TitleId);

        var exists = await _writeRepository
            .FindByIdAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        if (exists is null)
        {
            throw new TitleNotFoundException(command.TitleId);
        }

        await _writeRepository.DeleteAsync(titleId, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
