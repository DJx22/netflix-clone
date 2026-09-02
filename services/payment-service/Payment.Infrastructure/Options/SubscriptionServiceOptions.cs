namespace Payment.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the <c>SubscriptionService</c> configuration section.
/// Controls the ADR 0003 HTTP bridge: the URL Payment calls to activate a subscription
/// after a successful mock charge (Phases 1–3 only — replaced by RabbitMQ in Phase 4).
/// </summary>
public sealed class SubscriptionServiceOptions
{
    /// <summary>
    /// Base URL of the Subscription service.
    /// Default points to the local dev port (launchSettings.json "local" profile).
    /// Override via environment variable or appsettings for non-local environments.
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:5003";
}
