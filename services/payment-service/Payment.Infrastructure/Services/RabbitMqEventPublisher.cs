using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Application.Abstractions;
using Payment.Application.DTOs;
using Payment.Infrastructure.Options;
using RabbitMQ.Client;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Phase 4 implementation of <see cref="IEventPublisher"/>.
/// Publishes <c>PaymentCompleted</c> events to RabbitMQ using the raw
/// <c>RabbitMQ.Client</c> v7 — no MassTransit or other framework (ADR roadmap §Out-of-scope).
/// </summary>
/// <remarks>
/// <para>
/// <b>Topology (W1.4 contract):</b><br/>
/// Exchange  : <c>payment.events</c> (direct, durable)<br/>
/// Routing key: <c>payment.completed</c><br/>
/// Queue     : declared by the Subscription consumer on its startup
/// </para>
/// <para>
/// <b>Lifetime: Singleton.</b>
/// Holds one long-lived <see cref="IConnection"/> opened lazily on the first
/// publish.  Channels are lightweight and created per publish to avoid
/// sharing a non-thread-safe channel across concurrent requests.
/// <c>AutomaticRecoveryEnabled = true</c> lets the client library reconnect
/// and re-open channels after transient broker restarts without requiring
/// application-level retry logic (W2.9).
/// </para>
/// <para>
/// <b>Correlation ID propagation (W2.7):</b>
/// The <c>X-Correlation-Id</c> header value — set by
/// <c>CorrelationIdMiddleware</c> and stored in
/// <c>HttpContext.Items["CorrelationId"]</c> — is attached to every
/// published message as an AMQP header.  The Subscription consumer logs it
/// on receipt so the activation can be traced end-to-end through the event
/// hop.
/// </para>
/// <para>
/// <b>Failed-payment behaviour (W2.8):</b>
/// <c>PublishPaymentCompletedAsync</c> is called only from
/// <c>PaymentService.CreatePaymentAsync</c> inside the
/// <c>if (payment.IsSucceeded)</c> guard — a failed charge never reaches
/// this method.
/// </para>
/// </remarks>
public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    // W1.4 agreed routing key — must match the queue binding in the consumer.
    internal const string RoutingKey = "payment.completed";

    // Serializer options: camelCase to match the consumer's deserialization
    // settings and the W1.4 contract ("camelCase property names").
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RabbitMqOptions          _options;
    private readonly IHttpContextAccessor     _httpContextAccessor;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    // Lazily initialised on first publish; protected by _connectionLock.
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>Initialises a new <see cref="RabbitMqEventPublisher"/>.</summary>
    public RabbitMqEventPublisher(
        IOptions<RabbitMqOptions>      options,
        IHttpContextAccessor           httpContextAccessor,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _options             = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger              = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Opens a short-lived channel per call (channels are cheap).  The
    /// underlying <see cref="IConnection"/> is shared across calls via the
    /// lazy initialisation pattern.
    /// </remarks>
    public async Task PublishPaymentCompletedAsync(
        Guid paymentId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ResolveCorrelationId();

        var @event = new PaymentCompletedEvent(paymentId, subscriptionId, DateTime.UtcNow);
        var body   = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, _jsonOptions));

        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Declare the exchange idempotently — safe to call on every publish
        // because the broker ignores duplicate declarations with identical args.
        await channel.ExchangeDeclareAsync(
            exchange:   _options.ExchangeName,
            type:       ExchangeType.Direct,
            durable:    true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var props = new BasicProperties
        {
            ContentType  = "application/json",
            DeliveryMode = DeliveryModes.Persistent,   // survives broker restart
            MessageId    = Guid.NewGuid().ToString(),
            // W2.7 — attach correlation ID so the Subscription consumer can log it
            // for end-to-end tracing across the event hop.
            Headers      = new Dictionary<string, object?> { ["X-Correlation-Id"] = correlationId },
        };

        await channel.BasicPublishAsync(
            exchange:        _options.ExchangeName,
            routingKey:      RoutingKey,
            mandatory:       false,   // queue pre-declared by consumer; no return needed
            basicProperties: props,
            body:            body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published PaymentCompleted event for Payment {PaymentId} / Subscription {SubscriptionId} " +
            "with CorrelationId {CorrelationId}",
            paymentId, subscriptionId, correlationId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the existing <see cref="IConnection"/> if healthy, or creates a
    /// new one.  Thread-safe via <see cref="_connectionLock"/>.
    /// </summary>
    private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionLock.WaitAsync(ct);
        try
        {
            // Double-check inside the lock.
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName                 = _options.Host,
                Port                     = _options.Port,
                UserName                 = _options.UserName,
                Password                 = _options.Password,
                // AutomaticRecovery: the client reconnects after transient broker
                // interruptions without requiring application-level retry (W2.9).
                AutomaticRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(
                clientProvidedName: "payment-service-publisher",
                cancellationToken: ct);

            _logger.LogInformation(
                "RabbitMQ publisher connected to {Host}:{Port}", _options.Host, _options.Port);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Reads the correlation ID from <c>HttpContext.Items</c> where
    /// <c>CorrelationIdMiddleware</c> stored it.  Falls back to a new GUID
    /// for non-HTTP contexts (e.g. integration tests).
    /// </summary>
    private string ResolveCorrelationId()
    {
        var ctx = _httpContextAccessor.HttpContext;

        if (ctx?.Items.TryGetValue("CorrelationId", out var cid) == true
            && cid is string s
            && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}
