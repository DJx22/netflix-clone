using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPlaybackPositionRepository"/>.
/// </summary>
/// <remarks>
/// Lifetime: <b>Scoped</b> — holds a scoped <see cref="StreamingDbContext"/> (§7).
/// <para>
/// ADR 0004: entities retrieved by <see cref="FindAsync"/> are change-tracked within
/// the request scope. <see cref="UpsertAsync"/> calls <c>SaveChangesAsync</c> — EF
/// diffs only what <see cref="PlaybackPosition.UpdatePosition"/> actually changed.
/// </para>
/// </remarks>
public sealed class PlaybackPositionRepository : IPlaybackPositionRepository
{
    private readonly StreamingDbContext _dbContext;

    /// <summary>Initialises a new <see cref="PlaybackPositionRepository"/>.</summary>
    public PlaybackPositionRepository(StreamingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses the <c>FindAsync(object[], CancellationToken)</c> collection-literal overload
    /// introduced in EF Core 10. If the project is ever downgraded to EF 8/9,
    /// change to <c>FindAsync(new object[] { titleId, profileId }, cancellationToken)</c>.
    /// See <c>docs/backlog.md</c> §EF-FindAsync-Syntax.
    /// </remarks>
    public async Task<PlaybackPosition?> FindAsync(
        string titleId,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        // FindAsync with the composite key values in PK-declaration order:
        // (TitleId, ProfileId) as configured in PlaybackPositionConfiguration.
        return await _dbContext.PlaybackPositions
            .FindAsync([titleId, profileId], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Upsert strategy: the application service already determined whether the row
    /// existed (via <see cref="FindAsync"/> in the same Scoped DbContext).
    /// <list type="bullet">
    ///   <item>
    ///     <b>Existing row</b> (entity is tracked): <c>SaveChangesAsync</c> emits
    ///     an UPDATE for only the columns changed by
    ///     <see cref="PlaybackPosition.UpdatePosition"/>.
    ///   </item>
    ///   <item>
    ///     <b>New row</b> (entity is detached — freshly constructed in the service):
    ///     <c>_dbContext.PlaybackPositions.Add(position)</c> stages an INSERT and
    ///     <c>SaveChangesAsync</c> flushes it to SQL Server.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>§ConcurrentUpsertRace hardening (W3.4 — Phase 4):</b>
    /// If two concurrent requests both reach this method with a new (detached) entity
    /// for the same composite key <c>(TitleId, ProfileId)</c>, the second INSERT will
    /// fail with SQL Server error 2627 (PRIMARY KEY violation) or 2601 (unique index
    /// violation).  EF wraps this in a <see cref="DbUpdateException"/>.
    /// We catch that specific case, re-fetch the row the first thread inserted, apply
    /// the position update, and call <c>SaveChangesAsync</c> again — converting the
    /// race into a transparent UPDATE so the caller always succeeds instead of
    /// receiving HTTP 500.
    /// </para>
    /// </remarks>
    public async Task UpsertAsync(
        PlaybackPosition position,
        CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(position);

        if (entry.State == EntityState.Detached)
        {
            // New aggregate — stage INSERT.
            _dbContext.PlaybackPositions.Add(position);
        }
        // If already tracked (came from FindAsync in this scope), SaveChanges
        // diffs against the snapshot and issues UPDATE for changed columns only.

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            // §ConcurrentUpsertRace: a concurrent request won the INSERT race.
            // Detach the conflicting entry so EF stops tracking it, then re-fetch
            // the row that was committed and apply our update on top of it.
            _dbContext.Entry(position).State = EntityState.Detached;

            var existing = await _dbContext.PlaybackPositions
                .FindAsync([position.TitleId, position.ProfileId], cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                // Apply the same update to the winning row — idempotent if the
                // concurrent request wrote the same position value.
                existing.UpdatePosition(position.PositionSeconds, position.UpdatedAtUtc);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            // If re-fetch returns null (race with a concurrent delete — effectively
            // impossible in this service), the upsert becomes a no-op.
        }
    }
}
