using Catalog.Application.DTOs;
using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Creates a new title in the catalog.
/// Returns the full detail DTO so the API layer can build the 201 + Location response
/// without a separate round-trip.
/// </summary>
/// <remarks>
/// Fields mirror <c>UpsertTitleRequest</c> in the OpenAPI spec.  Strings are kept as
/// plain strings here — value-object construction is the aggregate's job, not the
/// command's; the command just carries the raw input from the API boundary.
/// </remarks>
public sealed record CreateTitleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> Genres,
    int ReleaseYear,
    string MaturityRating,
    IReadOnlyList<string> Cast,
    int DurationMinutes,
    string? PosterUrl,
    string? StreamingAssetId) : IRequest<TitleDetailDto>;
