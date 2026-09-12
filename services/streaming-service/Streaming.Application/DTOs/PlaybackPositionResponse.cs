namespace Streaming.Application.DTOs;

/// <summary>
/// Response DTO for <c>GET /api/v1/playback/{titleId}/position</c>.
/// Maps exactly to the OpenAPI <c>PlaybackPositionResponse</c> schema.
/// </summary>
/// <param name="TitleId">The title identifier.</param>
/// <param name="ProfileId">The profile whose position this record tracks.</param>
/// <param name="PositionSeconds">Resume position from the start, in whole seconds.</param>
/// <param name="UpdatedAtUtc">UTC instant at which this position was last written.</param>
public sealed record PlaybackPositionResponse(
    string   TitleId,
    Guid     ProfileId,
    int      PositionSeconds,
    DateTime UpdatedAtUtc);
