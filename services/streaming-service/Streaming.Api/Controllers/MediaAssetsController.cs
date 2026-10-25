using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streaming.Api.Middleware;
using Streaming.Application.DTOs;
using Streaming.Application.Services;

namespace Streaming.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for admin media-asset management (register and deregister).
/// All business logic lives in <see cref="IMediaAssetAdminService"/>; this class only
/// maps HTTP inputs to use-case calls and results back to HTTP responses (§11).
/// </summary>
/// <remarks>
/// Auth: <c>[Authorize]</c> — any valid JWT bearer is accepted for now.
/// Role gating (<c>AdminOnly</c> policy) is deferred to the role-based-access-control
/// future scope: <c>docs/future-scopes/role-based-access-control.md</c>.
/// </remarks>
[ApiController]
[Route(Routes.MediaAssetsBase)]
[Authorize]
public sealed class MediaAssetsController : ControllerBase
{
    private readonly IMediaAssetAdminService _adminService;

    /// <summary>Initialises a new <see cref="MediaAssetsController"/>.</summary>
    public MediaAssetsController(IMediaAssetAdminService adminService)
    {
        _adminService = adminService;
    }

    // -------------------------------------------------------------------------
    //  POST /api/v1/admin/media-assets
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers or updates a media asset for the given title.
    /// Upsert semantics — safe to call repeatedly; a second call with the same
    /// <c>titleId</c> updates the existing record rather than creating a duplicate.
    /// </summary>
    /// <param name="request">Media asset metadata.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="201">Asset created or updated. Location header points to the media GET endpoint.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Bearer token missing, expired, or invalid.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MediaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpsertMediaAsset(
        [FromBody] UpsertMediaAssetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService
            .UpsertAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // 201 with Location pointing to the read endpoint for this asset.
        return CreatedAtRoute(
            Routes.GetMediaRouteName,
            new { titleId = result.TitleId },
            result);
    }

    // -------------------------------------------------------------------------
    //  DELETE /api/v1/admin/media-assets/{titleId}
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deregisters the media asset for the given title.
    /// No-op when no asset is registered — always returns 204.
    /// </summary>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="204">Asset deleted (or was never registered).</response>
    /// <response code="401">Bearer token missing, expired, or invalid.</response>
    [HttpDelete("{titleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMediaAsset(
        string titleId,
        CancellationToken cancellationToken)
    {
        await _adminService
            .DeleteAsync(titleId, cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a 404 ProblemDetails response — kept for symmetry with PlaybackController
    /// in case future endpoints need it.
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
