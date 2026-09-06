using Catalog.Application.DTOs;
using Catalog.Application.Repositories;
using Catalog.Domain.Aggregates;
using Catalog.Domain.Exceptions;
using Catalog.Domain.ValueObjects;
using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Handles <see cref="CreateTitleCommand"/> by constructing a new <see cref="Title"/>
/// aggregate (which enforces all invariants), persisting it, then returning the detail DTO.
/// </summary>
/// <remarks>
/// The aggregate constructor is the single place invariants are checked — this handler
/// does not duplicate that logic.  If the aggregate throws a
/// <see cref="TitleValidationException"/>, the global exception middleware maps it to 400.
/// </remarks>
public sealed class CreateTitleCommandHandler : IRequestHandler<CreateTitleCommand, TitleDetailDto>
{
    private readonly ICatalogWriteRepository _writeRepository;

    public CreateTitleCommandHandler(ICatalogWriteRepository writeRepository)
    {
        _writeRepository = writeRepository;
    }

    public async Task<TitleDetailDto> Handle(
        CreateTitleCommand command,
        CancellationToken cancellationToken)
    {
        var titleId = TitleId.NewId();

        var genres = command.Genres
            .Select(g => new Genre(g))
            .ToList()
            .AsReadOnly();

        var title = new Title(
            titleId,
            command.Name,
            genres,
            command.ReleaseYear,
            new MaturityRating(command.MaturityRating),
            command.DurationMinutes,
            command.Description,
            command.Cast,
            command.PosterUrl,
            command.StreamingAssetId);

        await _writeRepository.AddAsync(title, cancellationToken).ConfigureAwait(false);

        return ToDetailDto(title);
    }

    // Mapping is intentionally co-located with the handler that owns the aggregate
    // reference — once the write side hands off to Infrastructure, the read side
    // projects from MongoDB directly and never calls this method.
    private static TitleDetailDto ToDetailDto(Title title) =>
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
