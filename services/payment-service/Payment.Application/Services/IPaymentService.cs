using Payment.Application.DTOs;
using Payment.Application.Exceptions;

namespace Payment.Application.Services;

/// <summary>
/// Application service for payment processing operations.
/// All methods map to one or more spec endpoints and delegate domain rules
/// to the <see cref="Payment.Domain.Aggregates.Payment"/> aggregate.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Returns all payments for the caller's subscription, ordered by
    /// <c>ProcessedAtUtc</c> descending (most recent first).
    /// Maps to <c>GET /api/v1/payments</c>.
    /// </summary>
    Task<IReadOnlyList<PaymentResponse>> GetPaymentsBySubscriptionIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single payment by its ID.
    /// Maps to <c>GET /api/v1/payments/{paymentId}</c>.
    /// </summary>
    /// <exception cref="PaymentNotFoundException">
    /// Thrown when no payment with the given ID exists. Maps to HTTP 404.
    /// </exception>
    Task<PaymentResponse> GetPaymentByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a mock charge and persists the result.
    /// Maps to <c>POST /api/v1/payments</c>.
    /// </summary>
    /// <remarks>
    /// Always returns a completed charge — status is either <c>Succeeded</c> or <c>Failed</c>.
    /// The <c>simulateFailure</c> field controls the mock outcome; when <see langword="null"/>
    /// the outcome is randomised (openapi.yaml createPayment description).
    /// On success, publishes <c>PaymentCompleted</c> to RabbitMQ so the Subscription service
    /// can activate the subscription (spec info.description). Publication is the responsibility
    /// of Infrastructure via <see cref="Abstractions.IEventPublisher"/>.
    /// </remarks>
    Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);
}
