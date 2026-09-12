using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Streaming.Api.Controllers;

/// <summary>
/// Readiness probe endpoint at <c>GET /health</c> (openapi.yaml).
/// No bearer token required — intentionally unauthenticated so that load
/// balancers and container orchestrators can poll without credentials.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    /// <summary>Initialises a new <see cref="HealthController"/>.</summary>
    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    // -------------------------------------------------------------------------
    //  GET /health
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns 200 when the service is ready to handle traffic.
    /// Runs all registered health checks (currently <c>streaming-db</c>).
    /// </summary>
    /// <response code="200">Service is healthy and ready.</response>
    /// <response code="503">One or more health checks are degraded or unhealthy.</response>
    [HttpGet(Routes.Health)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService
            .CheckHealthAsync(cancellationToken)
            .ConfigureAwait(false);

        return report.Status == HealthStatus.Healthy
            ? Ok(new { status = "Healthy" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = report.Status.ToString() });
    }
}
