namespace Payment.Application.Abstractions;

/// <summary>
/// Publishes domain events to the message broker.
/// </summary>
/// <remarks>
/// Implemented in <c>Payment.Infrastructure</c> using RabbitMQ.
/// Defined here so <c>Payment.Application</c> can depend on the abstraction
/// without referencing Infrastructure (§5 Dependency Inversion).
/// </remarks>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a <c>PaymentCompleted</c> event so the Subscription service
    /// can activate the subscription tied to this payment.
    /// </summary>
    /// <remarks>
    /// The spec states: "Publishes PaymentCompleted to RabbitMQ on success;
    /// that event, not this endpoint's response, is what Subscription actually waits on."
    /// Publication happens only when the charge outcome is <c>Succeeded</c> —
    /// a failed charge must not trigger subscription activation.
    /// </remarks>
    /// <param name="paymentId">The payment that just succeeded.</param>
    /// <param name="subscriptionId">The subscription to activate.</param>
    /// <param name="cancellationToken">Propagated from the originating request.</param>
    Task PublishPaymentCompletedAsync(
        Guid paymentId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default);
}
