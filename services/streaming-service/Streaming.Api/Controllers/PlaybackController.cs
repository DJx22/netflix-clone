using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streaming.Api.Middleware;
using Streaming.Application.DTOs;
using Streaming.Application.Results;
using Streaming.Application.Services;

namespace Streaming.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for the three playback use cases.
/// All business logic lives in <see cref="IPlaybackService"/>; this class only
/// maps HTTP inputs to use-case calls and typed results back to HTTP responses (§11).
/// </summary>
/// <remarks>
/// JWT bearer is required on all three endpoints (OpenAPI <c>bearerAuth</c> scheme).
/// No 404 exception is thrown here — not-found paths return a typed result and the
/// controller constructs the ProblemDetails response inline, consistent with the
/// <see cref="GetMediaResult"/> / <see cref="GetPositionResult"/> design decision.
/// </remarks>
[ApiController]
[Route(Routes.PlaybackBase)]
[Authorize]
public sealed class PlaybackController : ControllerBase
{
    private readonly IPlaybackService _playbackService;

    /// <summary>Initialises a new <see cref="PlaybackController"/>.</summary>
    public PlaybackController(IPlaybackService playbackService)
    {
        _playbackService = playbackService;
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/playback/{titleId}/media
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the media asset (URL, content type, duration) for the given title.
    /// </summary>
    /// <remarks>
    /// When no <c>MediaAsset</c> is registered in <c>StreamingDb</c>, the service
    /// performs a diagnostic lookup against Catalog (Application concern, not API)
    /// and returns 404 regardless of the Catalog result (ADR 0006 §3).
    /// </remarks>
    /// <param name="titleId">Title identifier from the route.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Media asset found.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No media asset registered for this title.</response>
    [HttpGet(Routes.Media)]
    [ProducesResponseType(typeof(MediaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedia(
        string titleId,
        CancellationToken cancellationToken)
    {
        var result = await _playbackService
            .GetMediaAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            GetMediaResult.Found f   => Ok(f.Response),
            GetMediaResult.NotFound  => NotFoundProblem($"No media asset found for title '{titleId}'."),
            _                        => throw new InvalidOperationException("Unexpected GetMediaResult subtype.")
        };
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/playback/{titleId}/position
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the saved playback position for the given title and profile.
    /// </summary>
    /// <param name="titleId">Title identifier from the route.</param>
    /// <param name="profileId">Profile identifier from the query string.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Position found.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No position has been saved for this title/profile pair.</response>
    [HttpGet(Routes.Position)]
    [ProducesResponseType(typeof(PlaybackPositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosition(
        string titleId,
        [FromQuery] Guid profileId,
        CancellationToken cancellationToken)
    {
        var result = await _playbackService
            .GetPositionAsync(titleId, profileId, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            GetPositionResult.Found f   => Ok(f.Response),
            GetPositionResult.NotFound  => NotFoundProblem(
                $"No playback position found for title '{titleId}' and profile '{profileId}'."),
            _                           => throw new InvalidOperationException("Unexpected GetPositionResult subtype.")
        };
    }

    // -------------------------------------------------------------------------
    //  PUT /api/v1/playback/{titleId}/position
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates or updates the playback position for the given title and profile.
    /// Upsert semantics — safe to call repeatedly as the player seeks.
    /// </summary>
    /// <param name="titleId">Title identifier from the route.</param>
    /// <param name="request">Position to save (validated by <c>SavePositionRequestValidator</c>).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="204">Position saved.</response>
    /// <response code="400">Validation failure (invalid profileId or negative positionSeconds).</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    [HttpPut(Routes.Position)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SavePosition(
        string titleId,
        [FromBody] SavePositionRequest request,
        CancellationToken cancellationToken)
    {
        await _playbackService
            .SavePositionAsync(titleId, request, cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a 404 ProblemDetails response with the correlation ID attached.
    /// Inline rather than thrown — typed results make exception-flow unnecessary
    /// for the expected not-found paths.
    /// </summary>
    private IActionResult NotFoundProblem(string detail)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        var problem = new ProblemDetails
        {
            Type     = "https://httpstatuses.com/404",
            Title    = "Not Found",
            Status   = StatusCodes.Status404NotFound,
            Detail   = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };

        return NotFound(problem);
    }
}
