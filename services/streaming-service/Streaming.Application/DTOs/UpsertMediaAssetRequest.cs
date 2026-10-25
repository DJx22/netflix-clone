namespace Streaming.Application.DTOs;

/// <summary>
/// Request body for <c>POST /api/v1/admin/media-assets</c>.
/// Upsert semantics — creates a new <c>MediaAsset</c> when none exists for
/// <see cref="TitleId"/>, or updates the existing one via <c>MediaAsset.Update</c>.
/// Validated by <c>UpsertMediaAssetRequestValidator</c> before reaching application logic.
/// </summary>
/// <param name="TitleId">
/// Identifier shared with Catalog's title. Must not be null or whitespace.
/// This service does not validate the ID against Catalog (ADR 0006 §3).
/// </param>
/// <param name="MediaUrl">
/// Complete URI of the pre-encoded media object (Azurite during local development).
/// Must be a non-empty, well-formed URI string.
/// </param>
/// <param name="ContentType">MIME type of the media object (e.g. <c>video/mp4</c>). Must not be empty.</param>
/// <param name="DurationSeconds">Total duration in whole seconds. Must be zero or positive.</param>
public sealed record UpsertMediaAssetRequest(
    string TitleId,
    string MediaUrl,
    string ContentType,
    int    DurationSeconds);
