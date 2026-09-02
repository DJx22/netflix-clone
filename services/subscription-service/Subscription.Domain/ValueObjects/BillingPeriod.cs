namespace Subscription.Domain.ValueObjects;

/// <summary>
/// Represents the end instant of a subscription billing period.
/// <para>
/// The spec field is named <c>currentPeriodEndUtc</c>; the domain enforces that
/// the value really is UTC so that comparisons are unambiguous regardless of where
/// the service runs.
/// </para>
/// </summary>
public sealed class BillingPeriod : IEquatable<BillingPeriod>
{
    /// <summary>Gets the UTC instant at which the current billing period ends.</summary>
    public DateTime EndUtc { get; }

    /// <summary>
    /// Initialises a <see cref="BillingPeriod"/> instance.
    /// </summary>
    /// <param name="endUtc">
    /// Must have <see cref="DateTimeKind.Utc"/>. Unspecified or local kinds are rejected
    /// to prevent silent time-zone bugs.
    /// </param>
    public BillingPeriod(DateTime endUtc)
    {
        if (endUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Billing period end must be a UTC DateTime (Kind == Utc).", nameof(endUtc));
        }

        EndUtc = endUtc;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the billing period has passed the given instant.
    /// </summary>
    /// <param name="utcNow">
    /// The current UTC instant. Must have <see cref="DateTimeKind.Utc"/>.
    /// </param>
    public bool HasExpired(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Comparison instant must be a UTC DateTime (Kind == Utc).", nameof(utcNow));
        }

        return utcNow >= EndUtc;
    }

    /// <inheritdoc/>
    public bool Equals(BillingPeriod? other) => other is not null && EndUtc == other.EndUtc;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BillingPeriod bp && Equals(bp);

    /// <inheritdoc/>
    public override int GetHashCode() => EndUtc.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => EndUtc.ToString("O");

    /// <summary>Equality operator.</summary>
    public static bool operator ==(BillingPeriod left, BillingPeriod right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(BillingPeriod left, BillingPeriod right) => !left.Equals(right);
}
