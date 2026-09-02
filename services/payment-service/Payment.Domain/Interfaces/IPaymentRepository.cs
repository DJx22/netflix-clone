using PaymentAggregate = Payment.Domain.Aggregates.Payment;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="PaymentAggregate"/> aggregate.
/// Implemented in <c>Payment.Infrastructure</c>; consumed by <c>Payment.Application</c>.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Returns the payment with the given ID, or <see langword="null"/> if it does not exist.
    /// </summary>
    Task<PaymentAggregate?> FindByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all payments for the given subscription, ordered by
    /// <c>ProcessedAtUtc</c> descending (most recent first).
    /// </summary>
    /// <remarks>
    /// Used by GET /api/v1/payments to return the caller's payment history.
    /// The spec returns a flat list with no pagination — this signature matches that shape.
    /// If pagination is added in a future phase, the interface should gain an overload
    /// rather than changing this method's signature, to avoid breaking existing callers.
    /// </remarks>
    Task<IReadOnlyList<PaymentAggregate>> FindBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly created <see cref="PaymentAggregate"/> aggregate.
    /// </summary>
    Task AddAsync(PaymentAggregate payment, CancellationToken cancellationToken = default);
}
