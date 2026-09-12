using Streaming.Domain.Entities;

namespace Streaming.Domain.Interfaces;

/// <summary>
/// Persistence contract for <see cref="MediaAsset"/>.
/// Implemented in <c>Streaming.Infrastructure</c>; consumed by <c>Streaming.Application</c>.
/// </summary>
/// <remarks>
/// Read-only surface: no create or update method is exposed because no write endpoint
/// for <see cref="MediaAsset"/> exists in the current API contract. Do not add one here
/// until a corresponding endpoint is defined in the OpenAPI spec.
/// </remarks>
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
}
