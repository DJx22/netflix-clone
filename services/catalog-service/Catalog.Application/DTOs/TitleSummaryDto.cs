namespace Catalog.Application.DTOs;

/// <summary>
/// Lightweight title representation returned by the search/list endpoint.
/// Shaped to match the OpenAPI spec's <c>TitleSummary</c> schema exactly —
/// the query handler projects from MongoDB directly into this type, never
/// materialising a full <c>Title</c> aggregate (ADR 0005).
/// </summary>
public sealed record TitleSummaryDto(
    string TitleId,
    string Name,
    IReadOnlyList<string> Genres,
    int ReleaseYear,
    string? PosterUrl);
