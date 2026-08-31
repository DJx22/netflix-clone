namespace Identity.Application.DTOs;

/// <summary>
/// Maps to openapi.yaml TokenResponse schema.
/// Returned by /login and /refresh.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>Short-lived JWT (15 min). Sent as bearer token to every downstream service.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Long-lived, single-use. Rotated on every /refresh call.</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>UTC instant at which the access token expires.</summary>
    public DateTime ExpiresAtUtc { get; init; }
}
