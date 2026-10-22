using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Subscription.Application.Exceptions;
using Subscription.Application.Services;
using Subscription.Domain.Exceptions;
using Subscription.Infrastructure.Messaging;
using Subscription.Infrastructure.Options;

namespace Subscription.Infrastructure.Messaging;

/// <summary>
/// Hosted service that consumes <c>PaymentCompleted</c> events from RabbitMQ
/// and activates subscriptions in-process via
/// <see cref="ISubscriptionService.ActivateSubscriptionAsync"/>.
/// </summary>
/// <remarks>Processing and delivery notes: <c>docs/payment-completed-event.md</c>.</remarks>
public sealed class PaymentCompletedConsumer : BackgroundService
{
    // This routing key must match what RabbitMqEventPublisher publishes.
    private const string RoutingKey = "payment.completed";

    // camelCase to match the publisher's serializer options.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly IOptions<RabbitMqOptions>         _options;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    /// <summary>Initialises a new <see cref="PaymentCompletedConsumer"/>.</summary>
    public PaymentCompletedConsumer(
        IServiceScopeFactory              scopeFactory,
        IOptions<RabbitMqOptions>         options,
        ILogger<PaymentCompletedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options      = options;
        _logger       = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Opens one connection + one channel for the lifetime of this hosted
    /// service.  AutomaticRecovery handles transient disconnects.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        var factory = new ConnectionFactory
        {
            HostName                 = opts.Host,
            Port                     = opts.Port,
            UserName                 = opts.UserName,
            Password                 = opts.Password,
            // AutomaticRecovery: reconnects + re-registers consumer after broker
            // restart — required for recovery-path behaviour.
            AutomaticRecoveryEnabled = true,
            // Note: DispatchConsumersAsync was removed in RabbitMQ.Client v7;
            // async consumer dispatch is now the default behaviour.
        };

        _logger.LogInformation(
            "PaymentCompletedConsumer connecting to RabbitMQ at {Host}:{Port}",
            opts.Host, opts.Port);

        await using var connection = await factory.CreateConnectionAsync(
            clientProvidedName: "subscription-service-consumer",
            cancellationToken:  stoppingToken);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // ── Declare topology ──────────────────────────────────────────────────
        await DeclareBrokerTopologyAsync(channel, opts, stoppingToken);

        // prefetchCount=1: process one message at a time; next is delivered only
        // after the current message is acked or nacked.  Keeps processing serial
        // and makes behaviour easier to reason about in this service.
        await channel.BasicQosAsync(
            prefetchSize:  0,
            prefetchCount: 1,
            global:        false,
            cancellationToken: stoppingToken);

        // ── Register consumer ─────────────────────────────────────────────────
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleDeliveryAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(
            queue:    opts.QueueName,
            autoAck:  false,   // manual ack — required for at-least-once guarantee
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "PaymentCompletedConsumer listening on queue '{Queue}'", opts.QueueName);

        // Keep the hosted service alive until the host signals shutdown.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        _logger.LogInformation("PaymentCompletedConsumer stopping.");
    }

    // ── Topology declaration ──────────────────────────────────────────────────

    /// <summary>
    /// Declares all required exchanges, queues, and bindings idempotently.
    /// Called once on startup before the consumer is registered.
    /// </summary>
    private static async Task DeclareBrokerTopologyAsync(
        IChannel channel, RabbitMqOptions opts, CancellationToken ct)
    {
        // 1. Dead-letter exchange (fanout — routes all nacked messages to the DLQ).
        //    Malformed or unprocessable messages land here instead of looping.
        await channel.ExchangeDeclareAsync(
            exchange:   opts.DeadLetterExchangeName,
            type:       ExchangeType.Fanout,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        // 2. Dead-letter queue — durable so messages survive broker restarts.
        await channel.QueueDeclareAsync(
            queue:      opts.DeadLetterQueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            cancellationToken: ct);

        // 3. Bind DLQ to DLX (fanout — no routing key required).
        await channel.QueueBindAsync(
            queue:      opts.DeadLetterQueueName,
            exchange:   opts.DeadLetterExchangeName,
            routingKey: string.Empty,
            cancellationToken: ct);

        // 4. Main exchange — direct, durable (matches publisher declaration).
        await channel.ExchangeDeclareAsync(
            exchange:   opts.ExchangeName,
            type:       ExchangeType.Direct,
            durable:    true,
            autoDelete: false,
            cancellationToken: ct);

        // 5. Main queue — durable with x-dead-letter-exchange pointing to the DLX.
        //    BasicNack(requeue: false) routes to the DLX automatically.
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = opts.DeadLetterExchangeName,
        };

        await channel.QueueDeclareAsync(
            queue:      opts.QueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  queueArgs,
            cancellationToken: ct);

        // 6. Bind main queue to main exchange with routing key.
        await channel.QueueBindAsync(
            queue:      opts.QueueName,
            exchange:   opts.ExchangeName,
            routingKey: RoutingKey,
            cancellationToken: ct);
    }

    // ── Message handler ───────────────────────────────────────────────────────

    /// <summary>
    /// Processes one <c>PaymentCompleted</c> delivery.
    /// Acks on success; nacks to DLQ on unrecoverable error.
    /// </summary>
    private async Task HandleDeliveryAsync(
        IChannel              channel,
        BasicDeliverEventArgs ea,
        CancellationToken     stoppingToken)
    {
        var correlationId = ReadCorrelationId(ea);

        _logger.LogInformation(
            "PaymentCompleted received — DeliveryTag={DeliveryTag} Redelivered={Redelivered} CorrelationId={CorrelationId}",
            ea.DeliveryTag, ea.Redelivered, correlationId);

        PaymentCompletedMessage? message;

        // ── Deserialize ───────────────────────────────────────────────────────
        try
        {
            message = JsonSerializer.Deserialize<PaymentCompletedMessage>(
                ea.Body.Span, _jsonOptions);
        }
        catch (JsonException ex)
        {
            // Malformed JSON is unrecoverable — nack to DLQ.
            _logger.LogError(ex,
                "Malformed PaymentCompleted payload — nacking to DLQ. " +
                "DeliveryTag={DeliveryTag} CorrelationId={CorrelationId}",
                ea.DeliveryTag, correlationId);

            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        if (message is null)
        {
            _logger.LogError(
                "PaymentCompleted deserialized to null — nacking to DLQ. " +
                "DeliveryTag={DeliveryTag} CorrelationId={CorrelationId}",
                ea.DeliveryTag, correlationId);

            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        // ── Activate subscription ─────────────────────────────────────────────
        // A new scope is created per message so SubscriptionService gets its own
        // Scoped DbContext (BackgroundService itself is Singleton-scoped).
        await using var scope = _scopeFactory.CreateAsyncScope();
        var subscriptionService = scope.ServiceProvider
            .GetRequiredService<ISubscriptionService>();

        try
        {
            await subscriptionService.ActivateSubscriptionAsync(
                message.SubscriptionId, stoppingToken);

            // Ack only after the database write succeeded (at-least-once guarantee).
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

            _logger.LogInformation(
                "Subscription {SubscriptionId} activated via PaymentCompleted. " +
                "PaymentId={PaymentId} CorrelationId={CorrelationId}",
                message.SubscriptionId, message.PaymentId, correlationId);
        }
        catch (SubscriptionNotFoundException ex)
        {
            // Unknown subscription — unrecoverable; nack to DLQ.
            _logger.LogError(ex,
                "SubscriptionNotFound for PaymentCompleted — nacking to DLQ. " +
                "SubscriptionId={SubscriptionId} PaymentId={PaymentId} CorrelationId={CorrelationId}",
                message.SubscriptionId, message.PaymentId, correlationId);

            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // InvalidSubscriptionOperationException (already Active →
            // domain returns silently; any other domain state → nack to DLQ).
            // For unexpected transient errors we also nack to avoid an infinite
            // redelivery loop.  Operators can inspect the DLQ and replay if needed.
            _logger.LogError(ex,
                "Failed to activate Subscription {SubscriptionId} — nacking to DLQ. " +
                "PaymentId={PaymentId} CorrelationId={CorrelationId}",
                message.SubscriptionId, message.PaymentId, correlationId);

            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts <c>X-Correlation-Id</c> from AMQP message headers.
    /// Returns a fallback GUID string if the header is absent or not a byte array.
    /// </summary>
    private static string ReadCorrelationId(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers?.TryGetValue("X-Correlation-Id", out var raw) == true
            && raw is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Guid.NewGuid().ToString();
    }
}
