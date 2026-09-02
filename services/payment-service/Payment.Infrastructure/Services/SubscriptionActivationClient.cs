using Payment.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Payment.Infrastructure.Services;

/// <summary>
/// ADR 0003 (Phases 1–3) implementation of <see cref="IEventPublisher"/>.
/// Calls <c>POST /api/v1/subscriptions/{subscriptionId}/activate</c> on the
/// Subscription service directly over HTTP, wrapped in Polly retry + circuit-breaker.
/// </summary>
/// <remarks>
/// This class is intentionally temporary. Phase 4 replaces it with a RabbitMQ
/// publisher; the <see cref="IEventPublisher"/> interface and the <see cref="Services.PaymentService"/>
/// call site remain unchanged — only this implementation is swapped.
/// <para>
/// Lifetime: <b>Scoped</b> — holds a logger that may carry per-request scope state.
/// The <see cref="HttpClient"/> itself is managed by <c>IHttpClientFactory</c> (Singleton
/// pool); only the wrapper class is Scoped (§7).
/// </para>
/// </remarks>
public sealed class SubscriptionActivationClient : IEventPublisher
{
    // Named client key — must match the name used in AddHttpClient<> registration.
    internal const string HttpClientName = "SubscriptionActivation";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubscriptionActivationClient> _logger;

    /// <summary>Initialises a new <see cref="SubscriptionActivationClient"/>.</summary>
    public SubscriptionActivationClient(
        IHttpClientFactory httpClientFactory,
        ILogger<SubscriptionActivationClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public async Task PublishPaymentCompletedAsync(
        Guid paymentId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        // POST body is empty — the subscriptionId is in the path per the Subscription
        // service's ADR 0003 endpoint definition (Routes.ActivateById).
        var response = await client
            .PostAsync(
                $"api/v1/subscriptions/{subscriptionId}/activate",
                content: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 409 means already active — idempotent per ADR 0004, treat as success.
            if ((int)response.StatusCode == 409)
            {
                _logger.LogInformation(
                    "Subscription {SubscriptionId} was already active when Payment {PaymentId} tried to activate it — treating as success",
                    subscriptionId,
                    paymentId);
                return;
            }

            _logger.LogError(
                "Subscription activation failed for Payment {PaymentId} / Subscription {SubscriptionId}. " +
                "HTTP {StatusCode}. The charge is recorded as Succeeded but the subscription remains PendingPayment.",
                paymentId,
                subscriptionId,
                (int)response.StatusCode);

            // Do not throw — the payment record is already persisted as Succeeded.
            // Throwing here would leave the caller with an unhandled exception after a
            // committed write, which is worse than the subscription staying PendingPayment.
            // A future Phase 4 outbox or retry mechanism should reconcile this gap.
        }
    }
}
