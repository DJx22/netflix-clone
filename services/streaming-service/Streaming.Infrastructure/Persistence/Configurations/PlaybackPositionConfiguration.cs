using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Streaming.Domain.Entities;

namespace Streaming.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table mapping for the <see cref="PlaybackPosition"/> entity.
/// </summary>
public sealed class PlaybackPositionConfiguration : IEntityTypeConfiguration<PlaybackPosition>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PlaybackPosition> builder)
    {
        builder.ToTable("PlaybackPositions");

        // Composite primary key via Fluent API — Data Annotations cannot express
        // composite keys and are explicitly excluded by the prompt.
        builder.HasKey(p => new { p.TitleId, p.ProfileId });

        builder.Property(p => p.TitleId)
            .IsRequired()
            .HasMaxLength(200)
            .ValueGeneratedNever();

        // Application generates the ProfileId — EF must not attempt to generate it.
        builder.Property(p => p.ProfileId)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(p => p.PositionSeconds)
            .IsRequired();

        // SQL Server datetime2 columns return DateTimeKind.Unspecified.
        // Re-assert UTC on read so PlaybackPosition.UpdatePosition's UTC guard stays satisfied
        // and callers receive correctly-kinded values throughout the stack.
        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired()
            .HasConversion(
                write => write,
                read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        // Secondary index for queries that filter by profile alone (e.g. "all positions for profile X").
        builder.HasIndex(p => p.ProfileId)
            .HasDatabaseName("IX_PlaybackPositions_ProfileId");
    }
}
