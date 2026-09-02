using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using Microsoft.EntityFrameworkCore;
using Subscription.Domain.Entities;

namespace Subscription.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Subscription bounded context.
/// <para>
/// Owns only the tables that belong to this service's database (ADR 0001 — one
/// database per service). Never references IdentityDb, CatalogDb, or any other
/// service's tables.
/// </para>
/// </summary>
/// <remarks>Lifetime: <b>Scoped</b> — DbContext accumulates per-request change state (§7).</remarks>
public sealed class SubscriptionDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="SubscriptionDbContext"/>.</summary>
    public SubscriptionDbContext(DbContextOptions<SubscriptionDbContext> options) : base(options) { }

    /// <summary>The Subscriptions table.</summary>
    public DbSet<SubscriptionAggregate> Subscriptions => Set<SubscriptionAggregate>();

    /// <summary>The Plans reference table.</summary>
    public DbSet<Plan> Plans => Set<Plan>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up SubscriptionConfiguration and PlanConfiguration automatically —
        // no need to register them one by one as the assembly grows.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionDbContext).Assembly);
    }
}
