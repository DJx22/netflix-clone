namespace Identity.Api;

/// <summary>
/// Named route constants for the Identity service.
/// Using named constants instead of magic strings satisfies §16 and ensures
/// <see cref="Controllers.AuthController"/> and <see cref="Controllers.HealthController"/>
/// stay in sync with openapi.yaml without duplication.
/// </summary>
internal static class Routes
{
    internal const string AuthBase = "api/v1/auth";

    internal const string Register = "register";
    internal const string Login    = "login";
    internal const string Refresh  = "refresh";
    internal const string Revoke   = "revoke";
    internal const string Me       = "me";

    /// <summary>
    /// Health endpoint at the service root (openapi.yaml line 167).
    /// Not nested under /api/v1/auth — it is a separate concern.
    /// </summary>
    internal const string Health = "/health";
}
