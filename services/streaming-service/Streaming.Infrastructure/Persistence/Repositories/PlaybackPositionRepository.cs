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
    ///     <c>_dbContext.Update(position)</c> attaches and marks all scalar properties
    ///     as Modified, then <c>SaveChangesAsync</c> emits an INSERT.
    ///     Using <c>Update</c> (not <c>Add</c>) here is intentional: if two concurrent
    ///     requests race to create the same composite-key row, SQL Server's PK constraint
    ///     will reject the second INSERT rather than producing a duplicate row.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>Phase hardening (see docs/backlog.md §ConcurrentUpsertRace):</b>
    /// A concurrent double-insert currently surfaces as a <c>DbUpdateException</c>
    /// mapped to HTTP 500 by the global exception handler. Before Phase 4 (high
    /// concurrent writes), catch <c>DbUpdateException</c> where the inner exception
    /// is a SQL Server uniqueness violation (error 2627 / 2601) and return 409 or
    /// silently retry.
    /// </para>
    /// </remarks>
    public async Task UpsertAsync(
        PlaybackPosition position,
        CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(position);

        if (entry.State == EntityState.Detached)
        {
            // New aggregate — attach it. EF will issue INSERT on SaveChanges.
            _dbContext.PlaybackPositions.Add(position);
        }
        // If already tracked (came from FindAsync in this scope), SaveChanges
        // diffs against the snapshot and issues UPDATE for changed columns only.

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
