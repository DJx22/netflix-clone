namespace Subscription.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the <c>RabbitMq</c> configuration section in
/// <c>Subscription.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Docker Compose injects these as env vars with the double-underscore separator:
/// <c>RabbitMq__Host=rabbitmq</c>, <c>RabbitMq__UserName=guest</c>, etc.
/// Local dev defaults (appsettings.local.json) point at <c>localhost:5672</c>.
/// </remarks>
public sealed class RabbitMqOptions
{
    /// <summary>Hostname or IP of the RabbitMQ broker. Default: <c>localhost</c>.</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>AMQP port. Default: <c>5672</c>.</summary>
    public int Port { get; init; } = 5672;

    /// <summary>Broker username. Default: <c>guest</c>.</summary>
    public string UserName { get; init; } = "guest";

    /// <summary>Broker password. Default: <c>guest</c>.</summary>
    public string Password { get; init; } = "guest";

    /// <summary>
    /// Name of the durable direct exchange published by the Payment service.
    /// Default: <c>payment.events</c> (Phase 4 contract).
    /// </summary>
    public string ExchangeName { get; init; } = "payment.events";

    /// <summary>
    /// Name of the durable queue this consumer declares and reads from.
    /// Default: <c>subscription.payment-completed</c> (Phase 4 contract).
    /// </summary>
    public string QueueName { get; init; } = "subscription.payment-completed";

    /// <summary>
    /// Name of the dead-letter exchange.  Unprocessable messages are nacked
    /// with requeue=false and routed here.
    /// Default: <c>payment.events.dlx</c>.
    /// </summary>
    public string DeadLetterExchangeName { get; init; } = "payment.events.dlx";

    /// <summary>
    /// Name of the dead-letter queue bound to <see cref="DeadLetterExchangeName"/>.
    /// Default: <c>subscription.payment-completed.dlq</c>.
    /// </summary>
    public string DeadLetterQueueName { get; init; } = "subscription.payment-completed.dlq";
}
