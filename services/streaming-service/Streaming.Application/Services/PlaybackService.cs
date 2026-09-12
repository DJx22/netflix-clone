using Microsoft.Extensions.Logging;

using Streaming.Application.Abstractions;
using Streaming.Application.DTOs;
using Streaming.Application.Mappings;
using Streaming.Application.Results;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Application.Services;

/// <summary>
/// Orchestrates the three playback use cases by coordinating repositories,
/// the <see cref="ICatalogClient"/>, and the clock abstraction.
/// Domain rules are enforced inside the entities; this class handles only
/// orchestration (query → mutate → persist → return result/DTO).
/// </summary>
public sealed class PlaybackService : IPlaybackService
{
    private readonly IMediaAssetRepository       _mediaAssetRepository;
    private readonly IPlaybackPositionRepository _playbackPositionRepository;
    private readonly ICatalogClient              _catalogClient;
    private readonly IDateTimeProvider           _dateTimeProvider;
    private readonly ILogger<PlaybackService>    _logger;

    /// <summary>Initialises a new <see cref="PlaybackService"/>.</summary>
    public PlaybackService(
        IMediaAssetRepository       mediaAssetRepository,
        IPlaybackPositionRepository playbackPositionRepository,
        ICatalogClient              catalogClient,
        IDateTimeProvider           dateTimeProvider,
        ILogger<PlaybackService>    logger)
    {
        _mediaAssetRepository       = mediaAssetRepository;
        _playbackPositionRepository = playbackPositionRepository;
        _catalogClient              = catalogClient;
        _dateTimeProvider           = dateTimeProvider;
        _logger                     = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Exact ADR 0006 §3 flow:
    /// <list type="number">
    ///   <item>Look up <c>titleId</c> in <c>StreamingDb</c>.</item>
    ///   <item>If found, return success — do not call Catalog.</item>
    ///   <item>
    ///     If not found, log the miss, then call <see cref="ICatalogClient"/> for the
    ///     diagnostic check. Log the Catalog result. In all three Catalog outcomes
    ///     (Found / NotFound / DependencyFailure), return <see cref="GetMediaResult.NotFound"/>.
    ///     Catalog is never a fallback source of media metadata.
    ///   </item>
    /// </list>
    /// </remarks>
    public async Task<GetMediaResult> GetMediaAsync(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _mediaAssetRepository
            .FindByTitleIdAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        if (asset is not null)
        {
            return GetMediaResult.Success(asset.ToResponse());
        }

        // --- post-miss diagnostic path (ADR 0006 §3) ---

        _logger.LogInformation(
            "MediaAsset not found in StreamingDb for {TitleId}. Checking Catalog for diagnostic.",
            titleId);

        var catalogResult = await _catalogClient
            .CheckTitleExistsAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        if (catalogResult.IsFound)
        {
            _logger.LogInformation(
                "TitleId {TitleId} exists in Catalog but has no MediaAsset in StreamingDb.",
                titleId);
        }
        else if (catalogResult.IsNotFound)
        {
            _logger.LogInformation(
                "TitleId {TitleId} was not found in Catalog.",
                titleId);
        }
        else
        {
            // DependencyFailure — Catalog was unreachable or returned an unexpected error.
            _logger.LogWarning(
                "Catalog check failed for {TitleId}. Returning 404 from Streaming.",
                titleId);
        }

        // Catalog result never changes the Streaming response.
        return GetMediaResult.Miss();
    }

    /// <inheritdoc/>
    public async Task<GetPositionResult> GetPositionAsync(
        string titleId,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var position = await _playbackPositionRepository
            .FindAsync(titleId, profileId, cancellationToken)
            .ConfigureAwait(false);

        if (position is not null)
        {
            return GetPositionResult.Success(position.ToResponse());
        }

        return GetPositionResult.Miss();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Upsert semantics: creates a new <see cref="PlaybackPosition"/> when none exists,
    /// or calls <see cref="PlaybackPosition.UpdatePosition"/> on the existing one.
    /// <see cref="IDateTimeProvider"/> supplies <c>UpdatedAtUtc</c> so the service
    /// does not take a static dependency on <see cref="DateTime.UtcNow"/>.
    /// </remarks>
    public async Task SavePositionAsync(
        string titleId,
        SavePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        var position = await _playbackPositionRepository
            .FindAsync(titleId, request.ProfileId, cancellationToken)
            .ConfigureAwait(false);

        if (position is not null)
        {
            // Existing record — mutate through the behaviour method (ADR 0004).
            position.UpdatePosition(request.PositionSeconds, now);
        }
        else
        {
            // No record yet — construct the aggregate for the first time.
            position = new PlaybackPosition(
                titleId,
                request.ProfileId,
                request.PositionSeconds,
                now);
        }

        await _playbackPositionRepository
            .UpsertAsync(position, cancellationToken)
            .ConfigureAwait(false);
    }
}
