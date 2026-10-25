using Microsoft.Extensions.Logging;
using Streaming.Application.DTOs;
using Streaming.Application.Mappings;
using Streaming.Domain.Entities;
using Streaming.Domain.Interfaces;

namespace Streaming.Application.Services;

/// <summary>
/// Orchestrates admin media-asset use cases: upsert and delete.
/// Domain rules are enforced inside <see cref="MediaAsset"/>; this class handles
/// only orchestration (find → create-or-update → persist → return DTO).
/// </summary>
public sealed class MediaAssetAdminService : IMediaAssetAdminService
{
    private readonly IMediaAssetRepository          _mediaAssetRepository;
    private readonly ILogger<MediaAssetAdminService> _logger;

    /// <summary>Initialises a new <see cref="MediaAssetAdminService"/>.</summary>
    public MediaAssetAdminService(
        IMediaAssetRepository           mediaAssetRepository,
        ILogger<MediaAssetAdminService> logger)
    {
        _mediaAssetRepository = mediaAssetRepository;
        _logger               = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Upsert flow:
    /// <list type="number">
    ///   <item>Look up existing asset by titleId — returns a change-tracked entity when found.</item>
    ///   <item>If found, call <see cref="MediaAsset.Update"/> then <c>UpdateAsync</c> (SaveChanges).</item>
    ///   <item>If not found, construct a new <see cref="MediaAsset"/> and call <c>AddAsync</c>.</item>
    /// </list>
    /// </remarks>
    public async Task<MediaResponse> UpsertAsync(
        UpsertMediaAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _mediaAssetRepository
            .FindByTitleIdAsync(request.TitleId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Updating existing MediaAsset for {TitleId}.", request.TitleId);

            // Mutate through the domain behaviour method (ADR 0004).
            // The entity is already change-tracked; UpdateAsync calls SaveChangesAsync.
            existing.Update(request.MediaUrl, request.ContentType, request.DurationSeconds);

            await _mediaAssetRepository
                .UpdateAsync(cancellationToken)
                .ConfigureAwait(false);

            return existing.ToResponse();
        }

        _logger.LogInformation(
            "Creating new MediaAsset for {TitleId}.", request.TitleId);

        var asset = new MediaAsset(
            request.TitleId,
            request.MediaUrl,
            request.ContentType,
            request.DurationSeconds);

        await _mediaAssetRepository
            .AddAsync(asset, cancellationToken)
            .ConfigureAwait(false);

        return asset.ToResponse();
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string titleId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting MediaAsset for {TitleId}.", titleId);

        await _mediaAssetRepository
            .DeleteAsync(titleId, cancellationToken)
            .ConfigureAwait(false);
    }
}
