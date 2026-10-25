using Streaming.Application.DTOs;

namespace Streaming.Application.Services;

/// <summary>
/// Use-case contract for admin media-asset management.
/// Implemented by <see cref="MediaAssetAdminService"/>; consumed by the API layer only.
/// </summary>
public interface IMediaAssetAdminService
{
    /// <summary>
    /// Creates or updates the <c>MediaAsset</c> for the title identified by
    /// <see cref="UpsertMediaAssetRequest.TitleId"/>.
    /// Upsert semantics: if an asset already exists it is updated via
    /// <c>MediaAsset.Update</c>; otherwise a new entity is created and persisted.
    /// </summary>
    /// <returns>The resulting <see cref="MediaResponse"/> after the upsert.</returns>
    Task<MediaResponse> UpsertAsync(UpsertMediaAssetRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the <c>MediaAsset</c> registered for <paramref name="titleId"/>.
    /// No-op when no asset is registered — callers receive 204 either way.
    /// </summary>
    Task DeleteAsync(string titleId, CancellationToken cancellationToken = default);
}
