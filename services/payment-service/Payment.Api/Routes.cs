namespace Payment.Api;

/// <summary>
/// Named route constants for the Payment service.
/// Using constants instead of inline string literals satisfies §16 and keeps
/// <see cref="Controllers.PaymentsController"/> and <see cref="Controllers.HealthController"/>
/// in sync with openapi.yaml.
/// </summary>
internal static class Routes
{
    /// <summary>Base path for all payment endpoints (openapi.yaml: /api/v1/payments).</summary>
    internal const string PaymentsBase = "api/v1/payments";

    /// <summary>
    /// Readiness probe — at the service root, not under api/v1 (openapi.yaml).
    /// </summary>
    internal const string Health = "/health";
}
