using Streaming.Domain.Entities;

namespace Streaming.Domain.Interfaces;

/// <summary>
/// Persistence contract for <see cref="MediaAsset"/>.
/// Implemented in <c>Streaming.Infrastructure</c>; consumed by <c>Streaming.Application</c>.
/// </summary>
public interface IMediaAssetRepository
{
    /// <summary>
    /// Returns the <see cref="MediaAsset"/> registered for <paramref name="titleId"/>,
    /// or <see langword="null"/> if Streaming has no media asset for that title.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> result means "no media asset registered in StreamingDb" —
    /// it does not imply the title is absent from Catalog (ADR 0006 §3).
    /// </remarks>
    /// <param name="titleId">The title identifier to look up.</param>
    /// <param name="cancellationToken">Propagated to the underlying data access call.</param>
    Task<MediaAsset?> FindByTitleIdAsync(string titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new <see cref="MediaAsset"/> record.
    /// Caller must ensure no asset already exists for <see cref="MediaAsset.TitleId"/>
    /// (use <see cref="FindByTitleIdAsync"/> first).
    /// </summary>
    Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists mutations made to an existing tracked <see cref="MediaAsset"/>
    /// (e.g. after calling <see cref="MediaAsset.Update"/>).
    /// </summary>
    Task UpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the <see cref="MediaAsset"/> for the given <paramref name="titleId"/>.
    /// No-op (does not throw) when no asset is registered for that title.
    /// </summary>
    Task DeleteAsync(string titleId, CancellationToken cancellationToken = default);
}

