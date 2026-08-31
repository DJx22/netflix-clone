using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API mapping for the <see cref="User"/> aggregate root.
/// Value objects are stored as owned types or primitive columns — no
/// domain types leak into the schema.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // UserId is a readonly record struct; store its inner Guid value.
        builder.Property(u => u.Id)
            .HasConversion(
                id => id.Value,
                value => UserId.From(value))
            .ValueGeneratedNever();

        // Email value object — store the normalised string.
        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => Email.From(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        // Unique constraint enforces the domain rule that one email maps to one account.
        // The domain cannot enforce this itself (requires a DB lookup).
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");

        // PasswordHash value object — store the opaque hash string.
        builder.Property(u => u.PasswordHash)
            .HasConversion(
                hash => hash.Value,
                value => PasswordHash.From(value))
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        // Owned collection: RefreshTokens live in their own table, FK back to Users.
        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(rt => rt.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
