using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Streaming.Domain.Entities;

namespace Streaming.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core table mapping for the <see cref="MediaAsset"/> entity.
/// </summary>
public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");

        builder.HasKey(a => a.TitleId);

        // Application populates TitleId — EF must not attempt to generate it.
        builder.Property(a => a.TitleId)
            .IsRequired()
            .HasMaxLength(200)
            .ValueGeneratedNever();

        // MediaUrl is a complete URI (OpenAPI format: uri). 2048 chars covers any
        // reasonable Azurite / Azure Blob Storage URL with SAS tokens.
        builder.Property(a => a.MediaUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.DurationSeconds)
            .IsRequired();
    }
}
