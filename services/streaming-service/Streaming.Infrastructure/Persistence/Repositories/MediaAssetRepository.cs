using Microsoft.EntityFrameworkCore;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IMediaAssetRepository"/>.
/// </summary>
/// <remarks>
/// Lifetime: <b>Scoped</b> — holds a scoped <see cref="StreamingDbContext"/> (§7).
/// <para>
/// Read-only: no write methods are exposed because no media-ingestion endpoint exists
/// in the current API contract. Do not add write methods until a corresponding endpoint
/// is defined in the OpenAPI spec.
/// </para>
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
}
