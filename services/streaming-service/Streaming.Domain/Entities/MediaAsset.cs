namespace Streaming.Domain.Entities;

/// <summary>
/// Stores the playback-specific media metadata Streaming owns for a given title.
/// </summary>
/// <remarks>
/// <para>
/// ADR 0006: Streaming owns <c>mediaUrl</c>, <c>contentType</c>, and <c>durationSeconds</c>.
/// Catalog owns descriptive title metadata. These are separate bounded contexts — this
/// entity must never reference or call into Catalog.
/// </para>
/// <para>
/// ADR 0004: plain-CRUD aggregate using EF Core native constructor binding.
/// One constructor only; parameter names match mapped property names case-insensitively.
/// Rename a property? Rename the matching constructor parameter too — EF fails at
/// model-build time, not compile time, if they diverge.
/// </para>
/// <para>
/// This entity is read-only from this service's API surface. No create/update endpoint
/// exists yet; do not add one. <see cref="MediaUrl"/> is a complete, pre-resolved URI —
/// no resolver or location-computation abstraction belongs here.
/// </para>
/// </remarks>
public sealed class MediaAsset
{
    /// <summary>Gets the title identifier. Shared with Catalog as a cross-service key.</summary>
    public string TitleId { get; private set; }

    /// <summary>
    /// Gets the URL of the pre-encoded media object (Azurite during local development).
    /// This is a complete URI as supplied in the OpenAPI schema (<c>format: uri</c>).
    /// </summary>
    public string MediaUrl { get; private set; }

    /// <summary>Gets the MIME type of the media object (e.g. <c>video/mp4</c>).</summary>
    public string ContentType { get; private set; }

    /// <summary>Gets the total duration of the media, in whole seconds. Zero or positive.</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>
    /// Initialises a <see cref="MediaAsset"/> and is also used by EF Core to materialise
    /// persisted rows. Parameter names must match the mapped property names exactly
    /// (case-insensitively) for EF's convention-based binding to resolve without
    /// additional configuration.
    /// </summary>
    /// <param name="titleId">The title identifier; must not be null or whitespace.</param>
    /// <param name="mediaUrl">
    /// The complete URI of the stored media object; must not be null or whitespace.
    /// </param>
    /// <param name="contentType">The MIME type of the media; must not be null or whitespace.</param>
    /// <param name="durationSeconds">Total duration in seconds; must be zero or positive.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when any string argument is null or whitespace, or when
    /// <paramref name="durationSeconds"/> is negative.
    /// </exception>
    public MediaAsset(
        string titleId,
        string mediaUrl,
        string contentType,
        int durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(titleId))
        {
            throw new ArgumentException("Title ID must not be null or whitespace.", nameof(titleId));
        }

        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            throw new ArgumentException("Media URL must not be null or whitespace.", nameof(mediaUrl));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type must not be null or whitespace.", nameof(contentType));
        }

        if (durationSeconds < 0)
        {
            throw new ArgumentException("Duration must be zero or positive.", nameof(durationSeconds));
        }

        TitleId         = titleId;
        MediaUrl        = mediaUrl;
        ContentType     = contentType;
        DurationSeconds = durationSeconds;
    }
}
