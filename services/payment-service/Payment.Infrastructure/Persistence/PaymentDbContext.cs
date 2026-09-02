using PaymentAggregate = Payment.Domain.Aggregates.Payment;

using Microsoft.EntityFrameworkCore;

namespace Payment.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Payment bounded context.
/// <para>
/// Owns only the tables that belong to this service's database (ADR 0001 — one
/// database per service). Never references IdentityDb, SubscriptionDb, or any other
/// service's tables.
/// </para>
/// </summary>
/// <remarks>Lifetime: <b>Scoped</b> — DbContext accumulates per-request change state (§7).</remarks>
public sealed class PaymentDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="PaymentDbContext"/>.</summary>
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    /// <summary>The Payments table.</summary>
    public DbSet<PaymentAggregate> Payments => Set<PaymentAggregate>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pick up PaymentConfiguration automatically —
        // no need to register it manually as the assembly grows.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
