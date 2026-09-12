using Streaming.Application.DTOs;
using Streaming.Application.Results;

namespace Streaming.Application.Services;

/// <summary>
/// Application service for playback use cases.
/// All three methods map directly to endpoints in <c>openapi.yaml</c>.
/// </summary>
public interface IPlaybackService
{
    /// <summary>
    /// Returns the media metadata for <paramref name="titleId"/> if a
    /// <c>MediaAsset</c> exists in <c>StreamingDb</c>.
    /// </summary>
    /// <remarks>
    /// If no <c>MediaAsset</c> is found, Streaming calls Catalog for a
    /// diagnostic existence check — for logging only. The Catalog result never
    /// changes the return value: a missing <c>MediaAsset</c> always yields
    /// <see cref="GetMediaResult.NotFound"/> (ADR 0006 §3).
    /// </remarks>
    /// <param name="titleId">Route parameter from <c>GET …/media</c>.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task<GetMediaResult> GetMediaAsync(
        string titleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the saved playback position for the (<paramref name="titleId"/>,
    /// <paramref name="profileId"/>) pair, or <see cref="GetPositionResult.NotFound"/>
    /// if none has been recorded yet.
    /// </summary>
    /// <param name="titleId">Route parameter from <c>GET …/position</c>.</param>
    /// <param name="profileId">Query parameter <c>profileId</c>.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task<GetPositionResult> GetPositionAsync(
        string titleId,
        Guid profileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the playback position for the (<c>titleId</c>, <c>profileId</c>) pair.
    /// Creates the record if it does not exist; updates it if it does.
    /// </summary>
    /// <param name="titleId">Route parameter from <c>PUT …/position</c>.</param>
    /// <param name="request">Validated request body.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    Task SavePositionAsync(
        string titleId,
        SavePositionRequest request,
        CancellationToken cancellationToken = default);
}
