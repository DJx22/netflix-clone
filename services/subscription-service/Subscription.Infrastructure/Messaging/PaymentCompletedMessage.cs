namespace Subscription.Infrastructure.Messaging;

/// <summary>
/// The deserialized payload of a <c>PaymentCompleted</c> RabbitMQ message
/// received by the Subscription consumer.
/// </summary>
/// <param name="PaymentId">
/// The payment that triggered this event.  Available for future idempotency
/// log entries (dedup table) if at-least-once is not sufficient.
/// </param>
/// <param name="SubscriptionId">
/// The subscription to activate.  Passed directly to
/// <c>ISubscriptionService.ActivateSubscriptionAsync</c>.
/// </param>
/// <param name="OccurredAtUtc">
/// UTC instant the payment was recorded as Succeeded on the publisher side.
/// </param>
/// <remarks>Contract and delivery notes: <c>docs/payment-completed-event.md</c>.</remarks>
public sealed record PaymentCompletedMessage(
    Guid     PaymentId,
    Guid     SubscriptionId,
    DateTime OccurredAtUtc);
