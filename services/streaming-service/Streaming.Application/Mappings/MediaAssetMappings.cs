using Streaming.Application.DTOs;
using Streaming.Domain.Entities;

namespace Streaming.Application.Mappings;

/// <summary>
/// Extension methods for mapping <see cref="MediaAsset"/> to response DTOs.
/// Internal — callers outside Application never receive domain types.
/// </summary>
internal static class MediaAssetMappings
{
    /// <summary>Projects a <see cref="MediaAsset"/> to a <see cref="MediaResponse"/>.</summary>
    internal static MediaResponse ToResponse(this MediaAsset asset) =>
        new(
            asset.TitleId,
            asset.MediaUrl,
            asset.ContentType,
            asset.DurationSeconds);
}
