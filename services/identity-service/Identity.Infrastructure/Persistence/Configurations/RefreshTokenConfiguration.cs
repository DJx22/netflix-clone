using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API mapping for the <see cref="RefreshToken"/> child entity.
/// Stored in its own table; always loaded alongside its owning <see cref="User"/>
/// via the Include in the repository — they form a single aggregate.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .ValueGeneratedNever();

        builder.Property(rt => rt.TokenValue)
            .HasMaxLength(512)
            .IsRequired();

        // Unique index so FindByRefreshTokenAsync can use a seek rather than a scan,
        // and to guard against the vanishingly-unlikely collision in token generation.
        builder.HasIndex(rt => rt.TokenValue)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenValue");

        // UserId FK — stored as Guid, converted back to UserId when read.
        builder.Property(rt => rt.UserId)
            .HasConversion(
                id => id.Value,
                value => UserId.From(value))
            .IsRequired();

        builder.Property(rt => rt.ExpiresAtUtc).IsRequired();
        builder.Property(rt => rt.CreatedAtUtc).IsRequired();
        builder.Property(rt => rt.IsRevoked).IsRequired();
    }
}
