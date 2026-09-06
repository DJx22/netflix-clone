namespace Catalog.Api.Requests;

/// <summary>
/// The request body for POST /api/v1/titles and PUT /api/v1/titles/{titleId}.
/// Matches the OpenAPI spec's <c>UpsertTitleRequest</c> schema exactly.
/// </summary>
/// <remarks>
/// Kept separate from <c>CreateTitleCommand</c> / <c>UpdateTitleCommand</c> so
/// that:
/// <list type="bullet">
///   <item>The controller maps the HTTP body to the correct command type,
///         making the create vs. update distinction explicit in the handler
///         rather than via a flag on a shared DTO.</item>
///   <item>The Application layer has no dependency on ASP.NET model-binding
///         attributes or conventions (§2).</item>
/// </list>
/// FluentValidation validators in <c>Catalog.Application</c> validate the
/// resulting command, not this raw request — this object is not validated
/// independently.
/// </remarks>
public sealed record UpsertTitleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Genres,
    int ReleaseYear,
    string MaturityRating,
    IReadOnlyList<string>? Cast,
    int DurationMinutes,
    string? PosterUrl,
    string? StreamingAssetId);
