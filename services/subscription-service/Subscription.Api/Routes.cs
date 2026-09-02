namespace Subscription.Api;

/// <summary>
/// Named route constants for the Subscription service.
/// Using constants instead of inline string literals satisfies §16 and keeps
/// <see cref="Controllers.PlansController"/>, <see cref="Controllers.SubscriptionsController"/>,
/// and <see cref="Controllers.HealthController"/> in sync with openapi.yaml.
/// </summary>
internal static class Routes
{
    internal const string PlansBase         = "api/v1/plans";

    internal const string SubscriptionsBase = "api/v1/subscriptions";

    /// <summary>Suffix for /me endpoints (get, cancel).</summary>
    internal const string Me                = "me";

    /// <summary>Suffix for plan-change endpoint.</summary>
    internal const string MePlan            = "me/plan";

    /// <summary>
    /// ADR 0003 Phase 1–3 bridge: Payment calls this to activate a subscription
    /// synchronously until the RabbitMQ consumer is in place (Phase 4).
    /// </summary>
    internal const string ActivateById      = "{subscriptionId:guid}/activate";

    /// <summary>
    /// Readiness probe — at the service root, not under api/v1 (openapi.yaml).
    /// </summary>
    internal const string Health            = "/health";
}
