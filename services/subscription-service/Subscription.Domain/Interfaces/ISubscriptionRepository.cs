using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

namespace Subscription.Domain.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="SubscriptionAggregate"/> aggregate.
/// Implemented in <c>Subscription.Infrastructure</c>; consumed by <c>Subscription.Application</c>.
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Returns the subscription with the given ID, or <see langword="null"/> if it does not exist.
    /// </summary>
    Task<SubscriptionAggregate?> FindByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the account's subscription that is in <c>Active</c> or <c>PendingPayment</c> status,
    /// or <see langword="null"/> if no such subscription exists.
    /// </summary>
    /// <remarks>
    /// Used to enforce the 409-conflict rule: an account may not have more than one
    /// Active or PendingPayment subscription at a time (spec §POST /api/v1/subscriptions).
    /// </remarks>
    Task<SubscriptionAggregate?> FindActiveOrPendingByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most-recent subscription for the account regardless of status,
    /// or <see langword="null"/> if the account has never had a subscription.
    /// </summary>
    /// <remarks>
    /// Used by GET /subscriptions/me, DELETE /subscriptions/me, and PUT /subscriptions/me/plan —
    /// all of which operate on "the caller's current subscription in whatever status it's in"
    /// (spec description for GET). Distinct from <see cref="FindActiveOrPendingByAccountIdAsync"/>,
    /// which is solely the 409-duplicate guard.
    /// </remarks>
    Task<SubscriptionAggregate?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly created <see cref="SubscriptionAggregate"/> aggregate.
    /// </summary>
    Task AddAsync(SubscriptionAggregate subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists state changes to an existing <see cref="SubscriptionAggregate"/> aggregate.
    /// </summary>
    Task UpdateAsync(SubscriptionAggregate subscription, CancellationToken cancellationToken = default);
}
