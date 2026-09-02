using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subscription.Domain.Entities;
using Subscription.Domain.ValueObjects;

namespace Subscription.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table mapping for the <see cref="Plan"/> reference entity.
/// <para>
/// Plan is not the aggregate root (Subscription is), but it is persisted in the
/// same bounded-context database (ADR 0001). Its value objects (<see cref="Money"/>,
/// <see cref="VideoQuality"/>) are mapped via value converters — EF calls the Plan
/// constructor with the already-converted objects during materialisation.
/// </para>
/// </summary>
public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasKey(p => p.PlanId);

        // Plans are seeded by the platform, not generated at runtime.
        builder.Property(p => p.PlanId)
            .IsRequired()
            .HasMaxLength(100)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.MaxProfiles).IsRequired();

        builder.Property(p => p.Description).HasMaxLength(500);

        // Money wraps decimal — stored as decimal(18,2) to match the monetary precision
        // of the spec's priceMonthly field. The converter unwraps/rewraps transparently;
        // EF passes the reconstructed Money instance to the Plan constructor.
        builder.Property(p => p.PriceMonthly)
            .IsRequired()
            .HasColumnType("decimal(18,2)")
            .HasConversion(
                write => write.Amount,
                read  => new Money(read));

        // VideoQuality wraps a string label (e.g. "SD", "HD", "4K").
        builder.Property(p => p.VideoQuality)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                write => write.Value,
                read  => new VideoQuality(read));
    }
}
