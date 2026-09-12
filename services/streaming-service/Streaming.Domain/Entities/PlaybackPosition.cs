namespace Streaming.Domain.Entities;

/// <summary>
/// Records the last-known playback position for a specific profile watching a specific title.
/// Identified by the composite key (<see cref="TitleId"/>, <see cref="ProfileId"/>).
/// </summary>
/// <remarks>
/// <para>
/// ADR 0006: persisted in <c>StreamingDb</c> (SQL Server). Positions are retained
/// indefinitely — no TTL, expiry timestamp, or retention policy is implemented.
/// <see cref="UpdatedAtUtc"/> is part of the data model; it is <em>not</em> an expiry marker.
/// </para>
/// <para>
/// ADR 0004: plain-CRUD aggregate using EF Core native constructor binding.
/// One constructor only; parameter names match mapped property names case-insensitively.
/// Rename a property? Rename the matching constructor parameter too — EF fails at
/// model-build time, not compile time, if they diverge.
/// </para>
/// <para>
/// State after construction is mutated exclusively through
/// <see cref="UpdatePosition(int, DateTime)"/>. No public setters are exposed.
/// </para>
/// </remarks>
public sealed class PlaybackPosition
{
    /// <summary>Gets the title identifier. Shared with Catalog as a cross-service key.</summary>
    public string TitleId { get; private set; }

    /// <summary>Gets the profile whose playback position this record tracks.</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>
    /// Gets the resume position within the media, in whole seconds from the start.
    /// Zero or positive; the OpenAPI schema enforces <c>minimum: 0</c>.
    /// </summary>
    public int PositionSeconds { get; private set; }

    /// <summary>
    /// Gets the UTC instant at which this position was last written.
    /// Informational only — not used as an expiry or retention cutoff (ADR 0006 §4).
    /// </summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Initialises a new <see cref="PlaybackPosition"/> and is also used by EF Core to
    /// materialise persisted rows. Parameter names must match the mapped property names
    /// exactly (case-insensitively) for EF's convention-based binding to resolve without
    /// additional configuration.
    /// </summary>
    /// <param name="titleId">The title identifier; must not be null or whitespace.</param>
    /// <param name="profileId">The profile identifier; must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="positionSeconds">Resume position in seconds; must be zero or positive.</param>
    /// <param name="updatedAtUtc">
    /// The UTC instant of this write. The Application layer supplies this from a clock
    /// abstraction — the Domain does not call <see cref="DateTime.UtcNow"/> directly.
    /// Must have <see cref="DateTime.Kind"/> == <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="titleId"/> is null or whitespace,
    /// <paramref name="positionSeconds"/> is negative, or
    /// <paramref name="updatedAtUtc"/> is not UTC.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="profileId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public PlaybackPosition(
        string titleId,
        Guid profileId,
        int positionSeconds,
        DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(titleId))
        {
            throw new ArgumentException("Title ID must not be null or whitespace.", nameof(titleId));
        }

        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile ID must not be empty.", nameof(profileId));
        }

        if (positionSeconds < 0)
        {
            throw new ArgumentException("Position must be zero or positive.", nameof(positionSeconds));
        }

        if (updatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Updated-at timestamp must be UTC.", nameof(updatedAtUtc));
        }

        TitleId         = titleId;
        ProfileId       = profileId;
        PositionSeconds = positionSeconds;
        UpdatedAtUtc    = updatedAtUtc;
    }

    /// <summary>
    /// Moves this position record to a new resume point.
    /// Called by the upsert use case after retrieving or creating the aggregate.
    /// </summary>
    /// <param name="positionSeconds">
    /// The new resume position in seconds; must be zero or positive
    /// (OpenAPI schema: <c>minimum: 0</c>).
    /// </param>
    /// <param name="updatedAtUtc">
    /// The UTC instant of this write, supplied by the Application layer.
    /// Must have <see cref="DateTime.Kind"/> == <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <remarks>
    /// No cross-entity validation against <c>MediaAsset.DurationSeconds</c> is performed
    /// here. Doing so would require a repository lookup inside the Domain, which is
    /// an anti-pattern. Any such guard belongs in the Application layer if required.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="positionSeconds"/> is negative or
    /// <paramref name="updatedAtUtc"/> is not UTC.
    /// </exception>
    public void UpdatePosition(int positionSeconds, DateTime updatedAtUtc)
    {
        if (positionSeconds < 0)
        {
            throw new ArgumentException("Position must be zero or positive.", nameof(positionSeconds));
        }

        if (updatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Updated-at timestamp must be UTC.", nameof(updatedAtUtc));
        }

        PositionSeconds = positionSeconds;
        UpdatedAtUtc    = updatedAtUtc;
    }
}
