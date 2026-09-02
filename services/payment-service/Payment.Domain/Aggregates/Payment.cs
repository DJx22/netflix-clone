using Payment.Domain.Enums;
using Payment.Domain.Exceptions;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Aggregates;

/// <summary>
/// The Payment aggregate root.
/// </summary>
/// <para>
/// Lifecycle: <c>Pending</c> → <c>Succeeded</c> or <c>Failed</c>.
/// Both terminal states are written in a single operation; a Payment never
/// stays in <c>Pending</c> for longer than the duration of one charge attempt
/// (openapi.yaml: "Mock charge processed — status Succeeded or Failed either way").
/// </para>
/// <remarks>
/// ADR 0001: IDs are UUIDs (Guid). EF Core binds this constructor by matching
/// parameter names to mapped property names, case-insensitively.
/// One constructor only — a second constructor reintroduces the ambiguity this pattern removes.
/// Rename a property? Rename the matching constructor parameter too, or EF fails
/// at model-build time, not compile time.
/// </remarks>
public sealed class Payment
{
    /// <summary>Gets the unique payment identifier (UUID per ADR 0001).</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Gets the subscription this payment is billing for.
    /// </summary>
    /// <remarks>
    /// Payment does not own the Subscription aggregate — it only holds the foreign key.
    /// Cross-aggregate navigation goes through the API or an event, never a direct object reference.
    /// </remarks>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Gets the charged amount (stored as a scalar for EF Core constructor binding — see ADR 0004).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the ISO 4217 currency code (stored as a scalar for EF Core constructor binding — see ADR 0004).</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the charged amount and currency as a <see cref="Money"/> value object.
    /// Computed from <see cref="Amount"/> and <see cref="Currency"/> — not mapped by EF Core.
    /// EF Core cannot bind owned entity types via constructor parameters; scalars are stored
    /// directly and this property reconstructs the value object on demand.
    /// </summary>
    public Money ChargedAmount => new Money(Amount, Currency);

    /// <summary>Gets the outcome of the charge attempt.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Gets the UTC instant at which the charge was processed.</summary>
    public DateTime ProcessedAtUtc { get; private set; }

    /// <summary>Optimistic-concurrency token. Set by EF Core; never touched by domain logic.</summary>
    public byte[] RowVersion { get; private set; } = default!;

    /// <summary>
    /// Returns <see langword="true"/> when the charge attempt has been created but not yet resolved.
    /// A <c>Pending</c> payment must never be persisted.
    /// </summary>
    public bool IsPending => Status == PaymentStatus.Pending;

    /// <summary>
    /// Returns <see langword="true"/> when the charge was accepted by the payment processor.
    /// </summary>
    public bool IsSucceeded => Status == PaymentStatus.Succeeded;

    /// <summary>
    /// Returns <see langword="true"/> when the charge was declined by the payment processor.
    /// </summary>
    public bool HasFailed => Status == PaymentStatus.Failed;

    /// <summary>
    /// Creates a new <see cref="Payment"/> in <c>Pending</c> status.
    /// The caller must call either <see cref="RecordSuccess"/> or <see cref="RecordFailure"/>
    /// before persisting.
    /// </summary>
    /// <remarks>
    /// EF Core uses this same constructor to materialise persisted rows by matching
    /// parameter names to property names. Properties not listed here (<c>Status</c>,
    /// <c>ProcessedAtUtc</c>, <c>RowVersion</c>) are written by EF via their
    /// private setters during materialisation, overwriting the values set below.
    /// </remarks>
    /// <param name="paymentId">A fresh, non-empty GUID supplied by the caller.</param>
    /// <param name="subscriptionId">The subscription being billed; must be non-empty.</param>
    /// <param name="amount">The charge amount; must be zero or greater.</param>
    /// <param name="currency">The ISO 4217 three-letter currency code.</param>
    /// <remarks>
    /// Parameter names (<c>paymentId</c>, <c>subscriptionId</c>, <c>amount</c>, <c>currency</c>)
    /// match the mapped property names case-insensitively — required for EF Core constructor
    /// binding (ADR 0004). Money invariants are still enforced via <see cref="Money"/>'s
    /// own constructor when <see cref="ChargedAmount"/> is computed.
    /// </remarks>
    public Payment(
        Guid paymentId,
        Guid subscriptionId,
        decimal amount,
        string currency)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID must not be empty.", nameof(paymentId));
        }

        if (subscriptionId == Guid.Empty)
        {
            throw new ArgumentException("Subscription ID must not be empty.", nameof(subscriptionId));
        }

        // Validate money invariants up-front via Money's own constructor
        // so the aggregate is never in an invalid monetary state.
        var money = new Money(amount, currency);

        PaymentId      = paymentId;
        SubscriptionId = subscriptionId;
        Amount         = money.Amount;
        Currency       = money.Currency;

        // Status starts as Pending; ProcessedAtUtc starts at MinValue.
        // Both are overwritten atomically by RecordSuccess or RecordFailure before the
        // aggregate is persisted — a Payment in Pending status must never reach the database.
        Status         = PaymentStatus.Pending;
        ProcessedAtUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Records a successful charge outcome and stamps the processing time.
    /// </summary>
    /// <param name="processedAtUtc">
    /// The UTC instant the charge was resolved. Application layer supplies this from
    /// <c>IDateTimeProvider.UtcNow</c> — the domain does not call <c>DateTime.UtcNow</c> directly.
    /// </param>
    /// <exception cref="InvalidPaymentOperationException">
    /// Thrown if the outcome has already been recorded.
    /// </exception>
    public void RecordSuccess(DateTime processedAtUtc)
    {
        EnsureOutcomeNotYetRecorded();
        ValidateProcessedAt(processedAtUtc);

        Status         = PaymentStatus.Succeeded;
        ProcessedAtUtc = processedAtUtc;
    }

    /// <summary>
    /// Records a failed charge outcome and stamps the processing time.
    /// </summary>
    /// <param name="processedAtUtc">
    /// The UTC instant the charge was resolved. Application layer supplies this from
    /// <c>IDateTimeProvider.UtcNow</c> — the domain does not call <c>DateTime.UtcNow</c> directly.
    /// </param>
    /// <exception cref="InvalidPaymentOperationException">
    /// Thrown if the outcome has already been recorded.
    /// </exception>
    public void RecordFailure(DateTime processedAtUtc)
    {
        EnsureOutcomeNotYetRecorded();
        ValidateProcessedAt(processedAtUtc);

        Status         = PaymentStatus.Failed;
        ProcessedAtUtc = processedAtUtc;
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private void EnsureOutcomeNotYetRecorded()
    {
        // Status == Pending is the only state in which an outcome can be recorded.
        // Once Succeeded or Failed, the record becomes permanent billing history.
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidPaymentOperationException(
                "Payment outcome has already been recorded and cannot be changed. " +
                "Payment records are immutable billing history.");
        }
    }

    private static void ValidateProcessedAt(DateTime processedAtUtc)
    {
        if (processedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Processed-at timestamp must be UTC.", nameof(processedAtUtc));
        }
    }
}
