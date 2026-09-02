using PaymentAggregate = Payment.Domain.Aggregates.Payment;

using Payment.Application.Abstractions;
using Payment.Application.DTOs;
using Payment.Application.Exceptions;
using Payment.Application.Mappings;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

/// <summary>
/// Orchestrates payment processing use cases by coordinating the repository,
/// the <see cref="PaymentAggregate"/> aggregate, the mock charge simulator,
/// the clock abstraction, and the event publisher.
/// All domain rules are enforced inside the aggregate; this class handles only
/// orchestration (build → charge → record outcome → persist → publish → return DTO).
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentSimulator _paymentSimulator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>Initialises a new <see cref="PaymentService"/>.</summary>
    public PaymentService(
        IPaymentRepository paymentRepository,
        IPaymentSimulator paymentSimulator,
        IDateTimeProvider dateTimeProvider,
        IEventPublisher eventPublisher)
    {
        _paymentRepository = paymentRepository;
        _paymentSimulator  = paymentSimulator;
        _dateTimeProvider  = dateTimeProvider;
        _eventPublisher    = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentResponse>> GetPaymentsBySubscriptionIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository
            .FindBySubscriptionIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        return payments
            .Select(p => p.ToResponse())
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> GetPaymentByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository
            .FindByIdAsync(paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment is null)
        {
            throw PaymentNotFoundException.ForPaymentId(paymentId);
        }

        return payment.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Pass amount and currency as scalars — the aggregate constructor now takes these
        // directly (ADR 0004 fix: EF Core cannot bind owned entity types via constructor params).
        // Money invariants (amount >= 0, currency 3 chars) are enforced inside the aggregate.
        var payment = new PaymentAggregate(
            Guid.NewGuid(),
            request.SubscriptionId,
            request.Amount,
            request.Currency);

        var processedAt = _dateTimeProvider.UtcNow;

        // The simulator resolves the mock charge — outcome is Succeeded or Failed.
        // simulateFailure is consumed entirely here and never stored on the aggregate.
        var succeeded = _paymentSimulator.Charge(request.SimulateFailure);

        if (succeeded)
        {
            payment.RecordSuccess(processedAt);
        }
        else
        {
            payment.RecordFailure(processedAt);
        }

        await _paymentRepository
            .AddAsync(payment, cancellationToken)
            .ConfigureAwait(false);

        // Only a successful charge triggers subscription activation.
        // A failed charge must not publish — Subscription must not activate on failure.
        if (payment.IsSucceeded)
        {
            await _eventPublisher
                .PublishPaymentCompletedAsync(payment.PaymentId, payment.SubscriptionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return payment.ToResponse();
    }
}
