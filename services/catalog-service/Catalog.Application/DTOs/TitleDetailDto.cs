namespace Catalog.Application.DTOs;

/// <summary>
/// Full title detail returned by the single-title GET endpoint.
/// Shaped to match the OpenAPI spec's <c>TitleDetailResponse</c> schema (a superset
/// of <c>TitleSummary</c>).  Projected straight from MongoDB without constructing a
/// <c>Title</c> aggregate (ADR 0005).
/// </summary>
public sealed record TitleDetailDto(
    string TitleId,
    string Name,
    IReadOnlyList<string> Genres,
    int ReleaseYear,
    string? PosterUrl,
    string? Description,
    IReadOnlyList<string> Cast,
    int DurationMinutes,
    string MaturityRating,
    string? StreamingAssetId);
