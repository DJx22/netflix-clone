using Streaming.Application.DTOs;
using Streaming.Domain.Entities;

namespace Streaming.Application.Mappings;

/// <summary>
/// Extension methods for mapping <see cref="PlaybackPosition"/> to response DTOs.
/// Internal — callers outside Application never receive domain types.
/// </summary>
internal static class PlaybackPositionMappings
{
    /// <summary>
    /// Projects a <see cref="PlaybackPosition"/> to a
    /// <see cref="PlaybackPositionResponse"/>.
    /// </summary>
    internal static PlaybackPositionResponse ToResponse(this PlaybackPosition position) =>
        new(
            position.TitleId,
            position.ProfileId,
            position.PositionSeconds,
            position.UpdatedAtUtc);
}
