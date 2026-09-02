using Microsoft.EntityFrameworkCore;
using Subscription.Domain.Entities;
using Subscription.Domain.Interfaces;

namespace Subscription.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IPlanRepository"/>.
/// </summary>
/// <remarks>Lifetime: <b>Scoped</b> — holds a scoped <see cref="SubscriptionDbContext"/> (§7).</remarks>
public sealed class PlanRepository : IPlanRepository
{
    private readonly SubscriptionDbContext _dbContext;

    /// <summary>Initialises a new <see cref="PlanRepository"/>.</summary>
    public PlanRepository(SubscriptionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<Plan?> FindByIdAsync(
        string planId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Plan>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        // Plans are a small, stable reference table — a full scan is correct here.
        // If the table grows large enough to need paging, add it to IPlanRepository at that point.
        return await _dbContext.Plans
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
