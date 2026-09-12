namespace Streaming.Application.DTOs;

/// <summary>
/// Response DTO for <c>GET /api/v1/playback/{titleId}/media</c>.
/// Maps exactly to the OpenAPI <c>MediaResponse</c> schema.
/// </summary>
/// <param name="TitleId">The title identifier.</param>
/// <param name="MediaUrl">
/// Complete URI of the pre-encoded media object.
/// Returned as-is from <c>StreamingDb</c> — no resolution or rewriting.
/// </param>
/// <param name="ContentType">MIME type of the media (e.g. <c>video/mp4</c>).</param>
/// <param name="DurationSeconds">Total duration in whole seconds. Zero or positive.</param>
public sealed record MediaResponse(
    string TitleId,
    string MediaUrl,
    string ContentType,
    int    DurationSeconds);
