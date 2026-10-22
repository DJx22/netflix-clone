namespace Payment.Application.DTOs;

/// <summary>
/// The message payload published to RabbitMQ when a payment charge succeeds.
/// </summary>
/// <param name="PaymentId">
/// The payment that just succeeded.  Used by the consumer for idempotency
/// checks (can be stored as a processed-event-id if a dedup log is added in
/// a future phase).
/// </param>
/// <param name="SubscriptionId">
/// The subscription to activate.  The Subscription consumer calls
/// <c>SubscriptionService.ActivateSubscriptionAsync(SubscriptionId)</c>.
/// </param>
/// <param name="OccurredAtUtc">
/// UTC instant at which the payment was recorded as Succeeded.  Set from
/// <c>IDateTimeProvider.UtcNow</c> in the publisher, not from the broker
/// timestamp, so the event is self-describing.
/// </param>
/// <remarks>Contract and delivery notes: <c>docs/payment-completed-event.md</c>.</remarks>
public sealed record PaymentCompletedEvent(
    Guid     PaymentId,
    Guid     SubscriptionId,
    DateTime OccurredAtUtc);
