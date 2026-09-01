using Microsoft.EntityFrameworkCore;
using Profile.Infrastructure.Persistence.Configurations;

namespace Profile.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for ProfileDb.
/// Scoped lifetime — one instance per request, disposed at the end of the request (§7).
/// Only this service's own tables live here (ADR 0001: one database per service).
/// </summary>
public sealed class ProfileDbContext : DbContext
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
    }
}
