# PaymentCompleted event

Payment publishes this event after a successful charge. Subscription consumes it
to activate the associated subscription. The publisher and consumer intentionally
keep separate message types, so changes to their fields or serialization must be
coordinated.

## Contract

| Field | Meaning |
|---|---|
| `PaymentId` | ID of the successful payment. |
| `SubscriptionId` | Subscription to activate. |
| `OccurredAtUtc` | UTC time the payment was recorded as successful. |

The publisher serializes with `System.Text.Json` using camelCase property names.
The consumer uses matching settings. The event carries the `X-Correlation-Id`
header from the originating request for logging across the event flow.

## RabbitMQ topology

| Component | Name | Configuration |
|---|---|---|
| Exchange | `payment.events` | Durable, direct |
| Routing key | `payment.completed` | |
| Queue | `subscription.payment-completed` | Durable |
| Dead-letter exchange | `payment.events.dlx` | Durable, fanout |
| Dead-letter queue | `subscription.payment-completed.dlq` | Durable |

The consumer declares the topology on startup. Messages rejected with
`requeue: false` route from the main queue to the dead-letter queue.

## Delivery behavior

Delivery is at-least-once: the consumer acknowledges only after activation
completes, so a crash before acknowledgement can result in redelivery. Activating
an already-active subscription is a no-op; other invalid states can fail.

Malformed or null payloads and missing subscriptions are rejected to the
dead-letter queue. Other processing exceptions are also rejected rather than
requeued, avoiding an infinite retry loop but requiring operators to inspect and
replay messages if appropriate.

The consumer processes one delivery at a time and creates a dependency-injection
scope per message so each activation uses its own scoped services and DbContext.
RabbitMQ client automatic recovery reconnects and restores the consumer after a
broker disconnect.
