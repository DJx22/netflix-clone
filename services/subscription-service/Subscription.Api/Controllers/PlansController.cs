using Microsoft.AspNetCore.Mvc;
using Subscription.Application.DTOs;
using Subscription.Application.Services;

namespace Subscription.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for plan listing.
/// All business logic lives in <see cref="IPlanService"/>; this class only maps
/// HTTP to a use-case call and the result back to HTTP (§11).
/// </summary>
/// <remarks>
/// No authentication required — browsing plans before signing up is an explicit
/// spec use case (openapi.yaml: "No auth required — browsing plans before signing up is a real flow.").
/// </remarks>
[ApiController]
[Route(Routes.PlansBase)]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    /// <summary>Initialises a new <see cref="PlansController"/>.</summary>
    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    /// <summary>List all available subscription plans.</summary>
    /// <response code="200">Plan list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlans(CancellationToken cancellationToken)
    {
        var plans = await _planService.ListPlansAsync(cancellationToken);
        return Ok(plans);
    }
}
