using Subscription.Application.DTOs;
using Subscription.Application.Exceptions;
using Subscription.Domain.Exceptions;

namespace Subscription.Application.Services;

/// <summary>
/// Application service for subscription lifecycle operations.
/// All methods map to one or more spec endpoints and delegate domain rules
/// to the <see cref="Subscription.Domain.Aggregates.Subscription"/> aggregate.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Returns the current subscription for the given account in whatever status it's in.
    /// </summary>
    /// <exception cref="SubscriptionNotFoundException">
    /// Thrown when the account has no subscription.
    /// Maps to HTTP 404 at the API boundary.
    /// </exception>
    Task<SubscriptionResponse> GetSubscriptionByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new subscription in <c>PendingPayment</c> status and returns 202.
    /// The subscription becomes <c>Active</c> only when a PaymentCompleted event
    /// is consumed from RabbitMQ — not synchronously here.
    /// </summary>
    /// <exception cref="PlanNotFoundException">Thrown when <c>planId</c> does not match a known plan.</exception>
    /// <exception cref="DuplicateSubscriptionException">Thrown when the account already has an Active or PendingPayment subscription.</exception>
    Task<SubscriptionResponse> CreateSubscriptionAsync(Guid accountId, CreateSubscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the account's current subscription.
    /// The cancellation is recorded immediately; access continues until <c>currentPeriodEndUtc</c>.
    /// </summary>
    /// <exception cref="SubscriptionNotFoundException">Thrown when the account has no subscription.</exception>
    /// <exception cref="InvalidSubscriptionOperationException">Thrown when the subscription cannot be cancelled from its current status.</exception>
    Task CancelSubscriptionAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the plan for the account's current subscription.
    /// </summary>
    /// <exception cref="SubscriptionNotFoundException">Thrown when the account has no subscription.</exception>
    /// <exception cref="PlanNotFoundException">Thrown when <c>newPlanId</c> does not match a known plan.</exception>
    /// <exception cref="InvalidSubscriptionOperationException">Thrown when the subscription status does not permit a plan change.</exception>
    Task<SubscriptionResponse> ChangePlanAsync(Guid accountId, ChangePlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a <c>PendingPayment</c> subscription to <c>Active</c>.
    /// Called by the PaymentCompleted RabbitMQ event handler — not by any HTTP endpoint.
    /// </summary>
    /// <param name="subscriptionId">The subscription to activate, as carried by the PaymentCompleted event.</param>
    /// <exception cref="SubscriptionNotFoundException">Thrown when no subscription with the given ID exists.</exception>
    /// <exception cref="InvalidSubscriptionOperationException">Thrown when the subscription is not in PendingPayment status.</exception>
    Task ActivateSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}
