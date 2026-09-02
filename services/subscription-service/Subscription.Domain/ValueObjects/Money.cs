namespace Subscription.Domain.ValueObjects;

/// <summary>
/// Represents a non-negative monetary amount for a plan price.
/// <para>
/// Uses <see cref="decimal"/> rather than <c>float</c> because the spec's
/// <c>number/float</c> is a JSON transport type; monetary arithmetic requires
/// exact decimal representation.
/// </para>
/// </summary>
public sealed class Money : IEquatable<Money>
{
    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>
    /// Initialises a <see cref="Money"/> instance.
    /// </summary>
    /// <param name="amount">Must be zero or positive; a plan cannot have a negative price.</param>
    public Money(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Plan price cannot be negative.");
        }

        Amount = amount;
    }

    /// <inheritdoc/>
    public bool Equals(Money? other) => other is not null && Amount == other.Amount;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Money m && Equals(m);

    /// <inheritdoc/>
    public override int GetHashCode() => Amount.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Amount.ToString("F2");

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Money left, Money right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Money left, Money right) => !left.Equals(right);
}
