namespace Profile.Api;

/// <summary>
/// Named route constants for the Profile service.
/// Using named constants instead of magic strings satisfies §16 and ensures
/// <see cref="Controllers.ProfilesController"/> and <see cref="Controllers.HealthController"/>
/// stay in sync with openapi.yaml without duplication.
/// </summary>
internal static class Routes
{
    internal const string ProfilesBase = "api/v1/profiles";
    internal const string ProfileById = "{profileId:guid}";

    /// <summary>
    /// Health endpoint at the service root (openapi.yaml line 165).
    /// Not nested under /api/v1/profiles — it is a separate concern.
    /// </summary>
    internal const string Health = "/health";
}
