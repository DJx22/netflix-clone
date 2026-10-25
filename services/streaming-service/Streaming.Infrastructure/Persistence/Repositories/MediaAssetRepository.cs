using Microsoft.EntityFrameworkCore;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IMediaAssetRepository"/>.
/// </summary>
/// <remarks>
/// Lifetime: <b>Scoped</b> — holds a scoped <see cref="StreamingDbContext"/> (§7).
/// </remarks>
public sealed class MediaAssetRepository : IMediaAssetRepository
{
    private readonly StreamingDbContext _dbContext;

    /// <summary>Initialises a new <see cref="MediaAssetRepository"/>.</summary>
    public MediaAssetRepository(StreamingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses the <c>FindAsync(object[], CancellationToken)</c> collection-literal overload
    /// introduced in EF Core 10. If the project is ever downgraded to EF 8/9,
    /// change this to <c>FindAsync(new object[] { titleId }, cancellationToken)</c>.
    /// See <c>docs/backlog.md</c> §EF-FindAsync-Syntax.
    /// </remarks>
    public async Task<MediaAsset?> FindByTitleIdAsync(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        // FindAsync uses the PK index — faster than FirstOrDefaultAsync for
        // single-key lookups and returns a tracked entity for free.
        return await _dbContext.MediaAssets
            .FindAsync([titleId], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        await _dbContext.MediaAssets
            .AddAsync(asset, cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The caller must first obtain the entity via <see cref="FindByTitleIdAsync"/>
    /// (which returns a change-tracked instance), mutate it via
    /// <see cref="MediaAsset.Update"/>, then call this method.
    /// EF Core detects the mutation automatically — no explicit DbSet.Update call needed.
    /// </remarks>
    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string titleId, CancellationToken cancellationToken = default)
    {
        var asset = await _dbContext.MediaAssets
            .FindAsync([titleId], cancellationToken)
            .ConfigureAwait(false);

        if (asset is null)
        {
            return; // no-op — asset was never registered or already deleted
        }

        _dbContext.MediaAssets.Remove(asset);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

