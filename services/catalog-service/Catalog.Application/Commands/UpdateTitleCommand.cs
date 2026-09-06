using Catalog.Application.DTOs;
using MediatR;

namespace Catalog.Application.Commands;

/// <summary>
/// Replaces all mutable metadata on an existing title.
/// Returns the updated detail DTO (matches the spec's PUT 200 response).
/// </summary>
/// <remarks>
/// The <paramref name="TitleId"/> is the route parameter; the remaining fields
/// mirror <c>UpsertTitleRequest</c> in the OpenAPI spec, identical to
/// <see cref="CreateTitleCommand"/> because the spec uses the same request body
/// for both create and update.
/// </remarks>
public sealed record UpdateTitleCommand(
    string TitleId,
    string Name,
    string? Description,
    IReadOnlyList<string> Genres,
    int ReleaseYear,
    string MaturityRating,
    IReadOnlyList<string> Cast,
    int DurationMinutes,
    string? PosterUrl,
    string? StreamingAssetId) : IRequest<TitleDetailDto>;
