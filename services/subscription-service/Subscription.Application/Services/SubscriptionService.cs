using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using Subscription.Application.Abstractions;
using Subscription.Application.DTOs;
using Subscription.Application.Exceptions;
using Subscription.Application.Mappings;
using Subscription.Domain.Exceptions;
using Subscription.Domain.Interfaces;

namespace Subscription.Application.Services;

/// <summary>
/// Orchestrates subscription lifecycle use cases by coordinating repositories,
/// the <see cref="SubscriptionAggregate"/> aggregate, and the clock abstraction.
/// All domain rules are enforced inside the aggregate; this class handles only
/// orchestration (query → mutate → persist → return DTO).
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>Initialises a new <see cref="SubscriptionService"/>.</summary>
    public SubscriptionService(
        ISubscriptionRepository subscriptionRepository,
        IPlanRepository planRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc/>
    public async Task<SubscriptionResponse> GetSubscriptionByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository
            .FindByAccountIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            throw SubscriptionNotFoundException.ForAccount(accountId);
        }

        return subscription.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<SubscriptionResponse> CreateSubscriptionAsync(
        Guid accountId,
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify the requested plan exists before creating anything.
        var plan = await _planRepository
            .FindByIdAsync(request.PlanId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            throw new PlanNotFoundException(request.PlanId);
        }

        // Enforce the 409 rule: one Active or PendingPayment subscription per account.
        var existing = await _subscriptionRepository
            .FindActiveOrPendingByAccountIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new DuplicateSubscriptionException(accountId);
        }

        var subscriptionId = Guid.NewGuid();

        // The domain constructor seeds CurrentPeriodEndUtc = createdAtUtc + 1 month
        // (monthly cadence inferred from the spec's priceMonthly field — ADR 0004).
        var subscription = new SubscriptionAggregate(
            subscriptionId,
            accountId,
            request.PlanId,
            _dateTimeProvider.UtcNow);

        await _subscriptionRepository
            .AddAsync(subscription, cancellationToken)
            .ConfigureAwait(false);

        return subscription.ToResponse();
    }

    /// <inheritdoc/>
    public async Task CancelSubscriptionAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository
            .FindByAccountIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            throw SubscriptionNotFoundException.ForAccount(accountId);
        }

        // Domain enforces that only Active or PastDue subscriptions can be cancelled.
        // InvalidSubscriptionOperationException propagates to the global error handler.
        subscription.Cancel();

        await _subscriptionRepository
            .UpdateAsync(subscription, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionResponse> ChangePlanAsync(
        Guid accountId,
        ChangePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository
            .FindByAccountIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            throw SubscriptionNotFoundException.ForAccount(accountId);
        }

        // Verify the target plan exists before mutating the aggregate.
        var plan = await _planRepository
            .FindByIdAsync(request.NewPlanId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            throw new PlanNotFoundException(request.NewPlanId);
        }

        // Domain enforces that PendingPayment and Cancelled subscriptions cannot change plan.
        subscription.ChangePlan(request.NewPlanId);

        await _subscriptionRepository
            .UpdateAsync(subscription, cancellationToken)
            .ConfigureAwait(false);

        return subscription.ToResponse();
    }

    /// <inheritdoc/>
    public async Task ActivateSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository
            .FindByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            // A PaymentCompleted event arrived for a subscription that does not exist.
            // This is unexpected and should surface as an error rather than be silently discarded
            // so that the event handler can dead-letter or alert rather than ack and lose the event.
            throw SubscriptionNotFoundException.ForSubscriptionId(subscriptionId);
        }

        // Domain enforces that only PendingPayment → Active is valid.
        subscription.Activate();

        await _subscriptionRepository
            .UpdateAsync(subscription, cancellationToken)
            .ConfigureAwait(false);
    }
}
