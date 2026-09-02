using PaymentAggregate = Payment.Domain.Aggregates.Payment;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Enums;

namespace Payment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table mapping for the <see cref="PaymentAggregate"/> aggregate.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentAggregate>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PaymentAggregate> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.PaymentId);

        // Application generates the ID — EF must not attempt to produce one (ADR 0001: UUIDs).
        builder.Property(p => p.PaymentId).ValueGeneratedNever();

        builder.Property(p => p.SubscriptionId).IsRequired();

        // Amount and Currency are stored as flat scalar columns.
        // ChargedAmount is a computed expression-body property that reconstructs Money
        // on demand from these two scalars — EF Core never maps it (Ignore below).
        builder.Property(p => p.Amount)
            .HasColumnName("Amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("Currency")
            .HasMaxLength(3)
            .IsRequired();

        // ChargedAmount is a computed get-only property — not a stored column.
        builder.Ignore(p => p.ChargedAmount);

        // Store enum name, not its integer ordinal: human-readable in queries and
        // survives reordering of enum members without a data migration.
        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        // SQL Server datetime2 columns come back with Kind = Unspecified.
        // Re-assert UTC so that callers receive a correctly-kinded value.
        builder.Property(p => p.ProcessedAtUtc)
            .IsRequired()
            .HasConversion(
                write => write,
                read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        // Optimistic concurrency token (ADR 0004).
        // EF generates the rowversion column in the migration; no application code touches it.
        builder.Property(p => p.RowVersion).IsRowVersion();

        // FindBySubscriptionIdAsync filters on SubscriptionId — index covers the history query.
        builder.HasIndex(p => p.SubscriptionId)
            .HasDatabaseName("IX_Payments_SubscriptionId");
    }
}
