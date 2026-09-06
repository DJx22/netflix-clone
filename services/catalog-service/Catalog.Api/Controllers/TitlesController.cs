using Catalog.Application.Commands;
using Catalog.Application.DTOs;
using Catalog.Application.Queries;
using Catalog.Api.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for title read and write operations.
/// All logic lives in MediatR command/query handlers — this class only maps
/// HTTP to a mediator dispatch and the result back to HTTP (§11, §16).
/// </summary>
[ApiController]
[Route(Routes.TitlesBase)]
public sealed class TitlesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Initialises a new <see cref="TitlesController"/>.</summary>
    public TitlesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/titles
    // -------------------------------------------------------------------------

    /// <summary>Search and list titles, with optional free-text and genre filters.</summary>
    /// <param name="search">Optional free-text term matched against the title name.</param>
    /// <param name="genre">Optional genre label to filter by.</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Page size (1–100, default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paged title summaries.</response>
    /// <response code="400">Invalid pagination parameters.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<TitleSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchTitles(
        [FromQuery] string? search,
        [FromQuery] string? genre,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query  = new SearchTitlesQuery(search, genre, page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/titles/{titleId}
    // -------------------------------------------------------------------------

    /// <summary>Get full detail for a single title.</summary>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Title detail.</response>
    /// <response code="404">No title with that ID.</response>
    [HttpGet(Routes.TitleById)]
    [ProducesResponseType(typeof(TitleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTitle(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        var query  = new GetTitleQuery(titleId);
        var result = await _mediator.Send(query, cancellationToken);

        // Handler returns null for "not found" rather than throwing, so the
        // controller owns the 404 decision — keeping HTTP concerns out of the
        // application layer (§11).
        return result is null
            ? NotFound()
            : Ok(result);
    }

    // -------------------------------------------------------------------------
    //  POST /api/v1/titles
    // -------------------------------------------------------------------------

    /// <summary>Add a new title.</summary>
    /// <param name="request">Title metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Title created; Location header points to the new resource.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Bearer token missing, expired, or invalid.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(TitleDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateTitle(
        [FromBody] UpsertTitleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateTitleCommand(
            request.Name,
            request.Description,
            request.Genres,
            request.ReleaseYear,
            request.MaturityRating,
            request.Cast ?? Array.Empty<string>(),
            request.DurationMinutes,
            request.PosterUrl,
            request.StreamingAssetId);

        var result = await _mediator.Send(command, cancellationToken);

        // 201 with Location pointing to the new title's GET endpoint.
        return CreatedAtAction(
            nameof(GetTitle),
            new { titleId = result.TitleId },
            result);
    }

    // -------------------------------------------------------------------------
    //  PUT /api/v1/titles/{titleId}
    // -------------------------------------------------------------------------

    /// <summary>Update a title's metadata.</summary>
    /// <param name="titleId">Title identifier (from route).</param>
    /// <param name="request">Replacement metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Updated title.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Bearer token missing, expired, or invalid.</response>
    /// <response code="404">No title with that ID.</response>
    [HttpPut(Routes.TitleById)]
    [Authorize]
    [ProducesResponseType(typeof(TitleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTitle(
        string titleId,
        [FromBody] UpsertTitleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateTitleCommand(
            titleId,
            request.Name,
            request.Description,
            request.Genres,
            request.ReleaseYear,
            request.MaturityRating,
            request.Cast ?? Array.Empty<string>(),
            request.DurationMinutes,
            request.PosterUrl,
            request.StreamingAssetId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  DELETE /api/v1/titles/{titleId}
    // -------------------------------------------------------------------------

    /// <summary>Remove a title.</summary>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Bearer token missing, expired, or invalid.</response>
    /// <response code="404">No title with that ID.</response>
    [HttpDelete(Routes.TitleById)]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTitle(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteTitleCommand(titleId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
