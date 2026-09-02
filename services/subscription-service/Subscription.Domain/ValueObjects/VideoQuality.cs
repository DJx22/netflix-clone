namespace Subscription.Domain.ValueObjects;

/// <summary>
/// Represents the video quality tier for a subscription plan (e.g. "SD", "HD", "4K").
/// <para>
/// The spec supplies examples but not an exhaustive enumeration; the domain enforces
/// non-emptiness only. Equality is case-insensitive so "hd" and "HD" are the same tier.
/// </para>
/// </summary>
public sealed class VideoQuality : IEquatable<VideoQuality>
{
    /// <summary>Gets the quality label exactly as stored.</summary>
    public string Value { get; }

    /// <summary>
    /// Initialises a <see cref="VideoQuality"/> instance.
    /// </summary>
    /// <param name="value">Must be non-null and non-whitespace.</param>
    public VideoQuality(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Video quality must not be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <inheritdoc/>
    public bool Equals(VideoQuality? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VideoQuality vq && Equals(vq);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(VideoQuality left, VideoQuality right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(VideoQuality left, VideoQuality right) => !left.Equals(right);
}
