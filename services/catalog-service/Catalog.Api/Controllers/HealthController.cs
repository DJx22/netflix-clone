using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.Controllers;

/// <summary>
/// Exposes GET /health as required by §11 and openapi.yaml.
/// Returns 200 only when the service is ready to serve reads and writes —
/// not merely that the process is alive.
/// </summary>
/// <remarks>
/// The readiness check pings the MongoDB database directly (registered in
/// <see cref="DependencyInjection"/> via <c>AddMongoHealthCheck</c>).  A liveness
/// probe — process-alive, no I/O — is separate and handled by the container
/// runtime (Phase 5 Kubernetes probes).
/// </remarks>
[ApiController]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    /// <summary>Initialises a new <see cref="HealthController"/>.</summary>
    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>Readiness probe.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Ready to serve traffic.</response>
    /// <response code="503">Not ready — MongoDB unreachable or degraded.</response>
    [HttpGet(Routes.Health)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        return report.Status == HealthStatus.Healthy
            ? Ok(new { status = "Healthy" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = report.Status.ToString() });
    }
}
