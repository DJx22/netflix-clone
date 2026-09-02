using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Subscription.Api.Controllers;

/// <summary>
/// Exposes GET /health as required by §11 and openapi.yaml.
/// Returns 200 only when the service is ready to serve traffic —
/// not merely that the process is alive.
/// <para>
/// The readiness check verifies that SubscriptionDb is reachable via EF Core.
/// A liveness probe (process-alive) is separate and provided by the container runtime.
/// </para>
/// </summary>
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
    /// <response code="200">Ready to serve traffic.</response>
    /// <response code="503">Not ready — SubscriptionDb unreachable or degraded.</response>
    [HttpGet(Routes.Health)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        return report.Status == HealthStatus.Healthy
            ? Ok(new { status = "Healthy" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = report.Status.ToString() });
    }
}
