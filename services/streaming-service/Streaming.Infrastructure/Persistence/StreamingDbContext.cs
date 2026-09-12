using Microsoft.EntityFrameworkCore;
using Streaming.Domain.Entities;

namespace Streaming.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Streaming bounded context.
/// <para>
/// Owns only the tables that belong to this service's database (ADR 0001 — one
/// database per service, <c>StreamingDb</c>). Never references any other service's tables.
/// </para>
/// </summary>
/// <remarks>Lifetime: <b>Scoped</b> — DbContext accumulates per-request change state (§7).</remarks>
public sealed class StreamingDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="StreamingDbContext"/>.</summary>
    public StreamingDbContext(DbContextOptions<StreamingDbContext> options) : base(options) { }

    /// <summary>The MediaAssets table — media metadata owned by Streaming (ADR 0006 §2).</summary>
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    /// <summary>The PlaybackPositions table — per-profile resume positions.</summary>
    public DbSet<PlaybackPosition> PlaybackPositions => Set<PlaybackPosition>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up MediaAssetConfiguration and PlaybackPositionConfiguration automatically —
        // no need to register them one by one as the assembly grows.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StreamingDbContext).Assembly);
    }
}
