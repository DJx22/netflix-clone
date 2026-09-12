using Streaming.Domain.Entities;

namespace Streaming.Domain.Interfaces;

/// <summary>
/// Persistence contract for <see cref="PlaybackPosition"/>.
/// Implemented in <c>Streaming.Infrastructure</c>; consumed by <c>Streaming.Application</c>.
/// </summary>
public interface IPlaybackPositionRepository
{
    /// <summary>
    /// Returns the saved playback position for the given title and profile,
    /// or <see langword="null"/> if no position has been recorded yet.
    /// </summary>
    /// <param name="titleId">The title identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <param name="cancellationToken">Propagated to the underlying data access call.</param>
    Task<PlaybackPosition?> FindAsync(
        string titleId,
        Guid profileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the <see cref="PlaybackPosition"/> record for its
    /// composite key (<c>TitleId</c>, <c>ProfileId</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named <c>Upsert</c> because the PUT endpoint has upsert semantics: the caller does
    /// not know whether a position record already exists. The Infrastructure implementation
    /// decides the exact SQL strategy (e.g. <c>MERGE</c>, <c>INSERT … ON CONFLICT</c>,
    /// or EF <c>AddOrUpdate</c>); the Domain contract only names the intent.
    /// </para>
    /// <para>
    /// The <paramref name="position"/> argument must have had
    /// <see cref="PlaybackPosition.UpdatePosition"/> called on it before this method
    /// is invoked — the repository does not mutate the aggregate.
    /// </para>
    /// </remarks>
    /// <param name="position">The aggregate to persist.</param>
    /// <param name="cancellationToken">Propagated to the underlying data access call.</param>
    Task UpsertAsync(PlaybackPosition position, CancellationToken cancellationToken = default);
}
