namespace Streaming.Application.DTOs;

/// <summary>
/// Request body for <c>PUT /api/v1/playback/{titleId}/position</c>.
/// Maps exactly to the OpenAPI <c>SavePositionRequest</c> schema.
/// Validated by <c>SavePositionRequestValidator</c> before reaching application logic.
/// </summary>
/// <param name="ProfileId">The profile whose position is being saved. Required, non-empty UUID.</param>
/// <param name="PositionSeconds">Resume position in whole seconds. Must be zero or positive.</param>
public sealed record SavePositionRequest(
    Guid ProfileId,
    int  PositionSeconds);
