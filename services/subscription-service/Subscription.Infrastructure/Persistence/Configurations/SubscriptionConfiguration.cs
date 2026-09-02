using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subscription.Domain.Enums;

namespace Subscription.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table mapping for the <see cref="SubscriptionAggregate"/> aggregate.
/// </summary>
public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<SubscriptionAggregate>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SubscriptionAggregate> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(s => s.SubscriptionId);

        // Application generates the ID — EF must not attempt to produce one.
        builder.Property(s => s.SubscriptionId).ValueGeneratedNever();

        builder.Property(s => s.AccountId).IsRequired();

        builder.Property(s => s.PlanId)
            .IsRequired()
            .HasMaxLength(100);

        // Store enum name, not its integer ordinal: human-readable in queries and
        // survives reordering of the SubscriptionStatus enum members.
        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        // SQL Server datetime2 columns come back with Kind = Unspecified.
        // Re-assert UTC so that callers (and RenewPeriod) receive a correctly-kinded value.
        builder.Property(s => s.CurrentPeriodEndUtc)
            .IsRequired()
            .HasConversion(
                write => write,
                read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired()
            .HasConversion(
                write => write,
                read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        // Optimistic concurrency token per ADR 0004.
        // EF generates the rowversion column in the migration; no application code touches it.
        builder.Property(s => s.RowVersion).IsRowVersion();

        // FindByAccountIdAsync and FindActiveOrPendingByAccountIdAsync both filter on AccountId.
        builder.HasIndex(s => s.AccountId).HasDatabaseName("IX_Subscriptions_AccountId");
    }
}
