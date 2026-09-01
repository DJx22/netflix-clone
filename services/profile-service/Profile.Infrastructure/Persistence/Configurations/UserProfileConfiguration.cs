using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Profile.Domain.Entities;
using Profile.Domain.ValueObjects;

namespace Profile.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API mapping for the <see cref="UserProfile"/> aggregate root.
/// Value objects are stored via conversions to primitive columns — no
/// domain types leak into the schema.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Profiles");

        builder.HasKey(p => p.Id);

        // ProfileId is a readonly record struct; store its inner Guid value.
        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => ProfileId.From(value))
            .HasColumnName("ProfileId")
            .ValueGeneratedNever();

        // AccountId foreign reference — stored as Guid, converted back to AccountId when read.
        builder.Property(p => p.AccountId)
            .HasConversion(
                id => id.Value,
                value => AccountId.From(value))
            .IsRequired();

        // Non-unique index on AccountId — all queries for a single account's profiles
        // use this column as the predicate.
        builder.HasIndex(p => p.AccountId)
            .HasDatabaseName("IX_Profiles_AccountId");

        // DisplayName value object — store the validated string.
        builder.Property(p => p.DisplayName)
            .HasConversion(
                name => name.Value,
                value => DisplayName.From(value))
            .HasMaxLength(DisplayName.MaxLength)
            .IsRequired();

        builder.Property(p => p.AvatarId)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(p => p.IsKidsProfile)
            .IsRequired();

        builder.Property(p => p.PreferredLanguage)
            .HasMaxLength(10)
            .IsRequired(false);

        builder.Property(p => p.MaturityRating)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();
    }
}
