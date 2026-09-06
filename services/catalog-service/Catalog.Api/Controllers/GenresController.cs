using Catalog.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for genre list operations.
/// All logic lives in the MediatR query handler — this class only dispatches
/// and maps the result (§11).
/// </summary>
[ApiController]
[Route(Routes.GenresBase)]
public sealed class GenresController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>Initialises a new <see cref="GenresController"/>.</summary>
    public GenresController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/genres
    // -------------------------------------------------------------------------

    /// <summary>List all distinct genre labels present in the catalog.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Array of genre label strings.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGenres(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListGenresQuery(), cancellationToken);
        return Ok(result);
    }
}
