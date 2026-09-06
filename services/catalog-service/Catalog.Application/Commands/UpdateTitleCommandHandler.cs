using Catalog.Application.DTOs;
using Catalog.Application.Repositories;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Handles <see cref="UpdateTitleCommand"/> by loading the <c>Title</c> aggregate,
/// calling <c>UpdateMetadata</c> (which re-enforces all invariants), then persisting.
/// </summary>
/// <remarks>
/// Loading through the write repository before mutating is the CQRS command pattern:
/// the full aggregate is always in memory when state changes, so invariants can be
/// checked against the complete current state, not a partial projection.
/// </remarks>
public sealed class UpdateTitleCommandHandler : IRequestHandler<UpdateTitleCommand, TitleDetailDto>
{
    private readonly ICatalogWriteRepository _writeRepository;

    public UpdateTitleCommandHandler(ICatalogWriteRepository writeRepository)
    {
        _writeRepository = writeRepository;
    }

    public async Task<TitleDetailDto> Handle(
        UpdateTitleCommand command,
        CancellationToken cancellationToken)
    {
        var titleId = new TitleId(command.TitleId);
        var title = await _writeRepository
            .FindByIdAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        if (title is null)
        {
            throw new TitleNotFoundException(command.TitleId);
        }

        var genres = command.Genres
            .Select(g => new Genre(g))
            .ToList()
            .AsReadOnly();

        title.UpdateMetadata(
            command.Name,
            genres,
            command.ReleaseYear,
            new MaturityRating(command.MaturityRating),
            command.DurationMinutes,
            command.Description,
            command.Cast,
            command.PosterUrl,
            command.StreamingAssetId);

        await _writeRepository.UpdateAsync(title, cancellationToken).ConfigureAwait(false);

        return ToDetailDto(title);
    }

    private static TitleDetailDto ToDetailDto(Domain.Aggregates.Title title) =>
        new(
            title.TitleId.Value,
            title.Name,
            title.Genres.Select(g => g.Value).ToList().AsReadOnly(),
            title.ReleaseYear,
            title.PosterUrl,
            title.Description,
            title.Cast,
            title.DurationMinutes,
            title.MaturityRating.Value,
            title.StreamingAssetId);
}
