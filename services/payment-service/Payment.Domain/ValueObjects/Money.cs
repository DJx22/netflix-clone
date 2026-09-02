namespace Payment.Domain.ValueObjects;

/// <summary>
/// Represents a non-negative monetary amount with its ISO 4217 currency code.
/// </summary>
/// <remarks>
/// The spec transports amount as <c>number/float</c> (openapi.yaml CreatePaymentRequest.amount),
/// but monetary arithmetic requires exact decimal representation — float accumulates
/// rounding error that would silently corrupt audit records.  The Application layer
/// is responsible for the float→decimal conversion at the boundary.
/// </remarks>
public sealed class Money : IEquatable<Money>
{
    /// <summary>Gets the monetary amount; always zero or positive.</summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets the ISO 4217 currency code (e.g. "USD").
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Initialises a <see cref="Money"/> instance.
    /// </summary>
    /// <param name="amount">Must be zero or greater; zero represents a free-tier or trial charge with no monetary movement.</param>
    /// <param name="currency">
    /// ISO 4217 three-letter code; must be non-empty.
    /// The domain does not validate the code against a full ISO list — that is a
    /// catalogue concern outside this bounded context.
    /// </param>
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            // Negative amounts have no valid billing meaning; zero is allowed for free-tier / trial records.
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount cannot be negative.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            // More than two decimal places cannot be represented in the column (decimal(18,2))
            // and would silently be truncated on write — reject early so there is no data loss.
            throw new ArgumentException(
                "Payment amount must not have more than 2 decimal places.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency code must not be empty.", nameof(currency));
        }

        if (currency.Length != 3)
        {
            // ISO 4217 codes are always exactly three uppercase letters.
            // Rejecting wrong-length codes catches the most common encoding mistakes early.
            throw new ArgumentException("Currency code must be a 3-character ISO 4217 code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    /// <inheritdoc/>
    public bool Equals(Money? other) =>
        other is not null &&
        Amount == other.Amount &&
        Currency == other.Currency;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Money m && Equals(m);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    /// <inheritdoc/>
    public override string ToString() => $"{Amount:F2} {Currency}";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Money left, Money right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Money left, Money right) => !left.Equals(right);
}
