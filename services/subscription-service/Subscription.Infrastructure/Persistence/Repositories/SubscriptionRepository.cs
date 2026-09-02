using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using Microsoft.EntityFrameworkCore;
using Subscription.Domain.Enums;
using Subscription.Domain.Interfaces;

namespace Subscription.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ISubscriptionRepository"/>.
/// </summary>
/// <remarks>
/// Lifetime: <b>Scoped</b> — holds a scoped <see cref="SubscriptionDbContext"/> (§7).
/// <para>
/// ADR 0004: entities are returned as tracked objects. Callers call a behaviour method
/// (e.g. <c>Cancel()</c>) then pass the entity to <see cref="UpdateAsync"/>; EF diffs
/// only what changed. No manual <c>Attach</c>, no manual entity-state assignment.
/// </para>
/// </remarks>
public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly SubscriptionDbContext _dbContext;

    /// <summary>Initialises a new <see cref="SubscriptionRepository"/>.</summary>
    public SubscriptionRepository(SubscriptionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<SubscriptionAggregate?> FindByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionAggregate?> FindByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        // Order by CreatedAtUtc descending so that a Cancelled subscription followed by
        // a new PendingPayment subscription returns the newer one, not the older one.
        return await _dbContext.Subscriptions
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionAggregate?> FindActiveOrPendingByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        // 409 guard — returns non-null if the account already has an Active or PendingPayment
        // subscription, so the caller can reject the creation attempt.
        return await _dbContext.Subscriptions
            .FirstOrDefaultAsync(
                s => s.AccountId == accountId &&
                     (s.Status == SubscriptionStatus.Active ||
                      s.Status == SubscriptionStatus.PendingPayment),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(
        SubscriptionAggregate subscription,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Subscriptions
            .AddAsync(subscription, cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        SubscriptionAggregate subscription,
        CancellationToken cancellationToken = default)
    {
        // The entity was loaded by FindBy*Async in the same scoped DbContext — it is
        // already tracked. SaveChangesAsync emits an UPDATE for only the changed columns
        // and checks the RowVersion concurrency token (ADR 0004).
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
