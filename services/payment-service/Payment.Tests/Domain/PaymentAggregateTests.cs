using Payment.Domain.Enums;
using Payment.Domain.Exceptions;
using PaymentAggregate = Payment.Domain.Aggregates.Payment;

namespace Payment.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Payment"/> aggregate root.
/// Covers construction invariants, status transitions, guard methods,
/// and the computed <c>ChargedAmount</c> property.
/// </summary>
public sealed class PaymentAggregateTests
{
    // ── Shared test data ──────────────────────────────────────────────────────

    private static readonly Guid ValidPaymentId       = Guid.NewGuid();
    private static readonly Guid ValidSubscriptionId  = Guid.NewGuid();
    private static readonly DateTime ValidProcessedAt = DateTime.UtcNow;

    // ── Constructor: ID guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyPaymentId_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new PaymentAggregate(Guid.Empty, ValidSubscriptionId, 10m, "USD");

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("paymentId", ex.ParamName);
    }

    [Fact]
    public void Constructor_EmptySubscriptionId_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new PaymentAggregate(ValidPaymentId, Guid.Empty, 10m, "USD");

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("subscriptionId", ex.ParamName);
    }

    // ── Constructor: Money delegation ─────────────────────────────────────────
    // Money's own constructor throws on invalid input; the aggregate delegates to it.

    [Fact]
    public void Constructor_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act
        Action act = () => _ = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, -1m, "USD");

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Constructor_InvalidCurrencyLength_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "US");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_EmptyCurrency_ThrowsArgumentException()
    {
        // Arrange / Act
        Action act = () => _ = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, string.Empty);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    // ── Constructor: valid construction ───────────────────────────────────────

    [Fact]
    public void Constructor_ValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange / Act
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 9.99m, "usd");

        // Assert
        Assert.Equal(ValidPaymentId,      payment.PaymentId);
        Assert.Equal(ValidSubscriptionId, payment.SubscriptionId);
        Assert.Equal(9.99m,              payment.Amount);
        Assert.Equal("USD",              payment.Currency);   // normalised to upper-case by Money
    }

    [Fact]
    public void Constructor_ValidArguments_StatusIsPending()
    {
        // Arrange / Act
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");

        // Assert — Pending is the sole valid initial state; must never be persisted
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Constructor_ValidArguments_ProcessedAtIsMinValue()
    {
        // Arrange / Act
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");

        // Assert — MinValue sentinel; overwritten by RecordSuccess/RecordFailure before persist
        Assert.Equal(DateTime.MinValue, payment.ProcessedAtUtc);
    }

    [Fact]
    public void Constructor_ZeroAmount_DoesNotThrow()
    {
        // Arrange / Act / Assert — zero is valid (free-tier / trial charge per Money contract)
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 0m, "USD");
        Assert.Equal(0m, payment.Amount);
    }

    // ── Status helper properties ──────────────────────────────────────────────

    [Fact]
    public void IsPending_WhenNewlyConstructed_ReturnsTrue()
    {
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        Assert.True(payment.IsPending);
    }

    [Fact]
    public void IsSucceeded_WhenNewlyConstructed_ReturnsFalse()
    {
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        Assert.False(payment.IsSucceeded);
    }

    [Fact]
    public void HasFailed_WhenNewlyConstructed_ReturnsFalse()
    {
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        Assert.False(payment.HasFailed);
    }

    [Fact]
    public void IsSucceeded_AfterRecordSuccess_ReturnsTrue()
    {
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordSuccess(ValidProcessedAt);
        Assert.True(payment.IsSucceeded);
    }

    [Fact]
    public void HasFailed_AfterRecordFailure_ReturnsTrue()
    {
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordFailure(ValidProcessedAt);
        Assert.True(payment.HasFailed);
    }

    // ── RecordSuccess ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordSuccess_OnPendingPayment_SetsStatusSucceeded()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");

        // Act
        payment.RecordSuccess(ValidProcessedAt);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void RecordSuccess_OnPendingPayment_StampsProcessedAtUtc()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        var processedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        payment.RecordSuccess(processedAt);

        // Assert
        Assert.Equal(processedAt, payment.ProcessedAtUtc);
    }

    [Fact]
    public void RecordSuccess_NonUtcTimestamp_ThrowsArgumentException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        var localTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        Action act = () => payment.RecordSuccess(localTime);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal("processedAtUtc", ex.ParamName);
    }

    [Fact]
    public void RecordSuccess_UnspecifiedTimestamp_ThrowsArgumentException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        var unspecified = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        Action act = () => payment.RecordSuccess(unspecified);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void RecordSuccess_OnAlreadySucceeded_ThrowsInvalidPaymentOperationException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordSuccess(ValidProcessedAt);

        // Act — payment records are immutable billing history once resolved
        Action act = () => payment.RecordSuccess(ValidProcessedAt);

        // Assert
        Assert.Throws<InvalidPaymentOperationException>(act);
    }

    [Fact]
    public void RecordSuccess_OnAlreadyFailed_ThrowsInvalidPaymentOperationException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordFailure(ValidProcessedAt);

        // Act
        Action act = () => payment.RecordSuccess(ValidProcessedAt);

        // Assert
        Assert.Throws<InvalidPaymentOperationException>(act);
    }

    // ── RecordFailure ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordFailure_OnPendingPayment_SetsStatusFailed()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");

        // Act
        payment.RecordFailure(ValidProcessedAt);

        // Assert
        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public void RecordFailure_OnPendingPayment_StampsProcessedAtUtc()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        var processedAt = new DateTime(2025, 7, 15, 8, 30, 0, DateTimeKind.Utc);

        // Act
        payment.RecordFailure(processedAt);

        // Assert
        Assert.Equal(processedAt, payment.ProcessedAtUtc);
    }

    [Fact]
    public void RecordFailure_NonUtcTimestamp_ThrowsArgumentException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        var localTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        Action act = () => payment.RecordFailure(localTime);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void RecordFailure_OnAlreadySucceeded_ThrowsInvalidPaymentOperationException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordSuccess(ValidProcessedAt);

        // Act
        Action act = () => payment.RecordFailure(ValidProcessedAt);

        // Assert
        Assert.Throws<InvalidPaymentOperationException>(act);
    }

    [Fact]
    public void RecordFailure_OnAlreadyFailed_ThrowsInvalidPaymentOperationException()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 10m, "USD");
        payment.RecordFailure(ValidProcessedAt);

        // Act
        Action act = () => payment.RecordFailure(ValidProcessedAt);

        // Assert
        Assert.Throws<InvalidPaymentOperationException>(act);
    }

    // ── ChargedAmount computed property ───────────────────────────────────────

    [Fact]
    public void ChargedAmount_ReturnsMoneyReconstructedFromScalars()
    {
        // Arrange
        var payment = new PaymentAggregate(ValidPaymentId, ValidSubscriptionId, 15.75m, "gbp");

        // Act
        var charged = payment.ChargedAmount;

        // Assert — currency normalised to upper-case by Money constructor
        Assert.Equal(15.75m, charged.Amount);
        Assert.Equal("GBP",  charged.Currency);
    }

    // ── Process-constraint note (not an aggregate invariant) ──────────────────
    // ⚠ Gap noted: The spec states "a Payment in Pending status must never be persisted."
    // This is enforced at the Application layer (PaymentService always calls RecordSuccess
    // or RecordFailure before AddAsync), not by an aggregate-level guard.
    // There is no domain method that prevents calling AddAsync on a Pending instance directly.
    // If this constraint should move into the domain, the aggregate would need a
    // ValidateForPersistence() or similar method. Flagged here for future consideration.
}
