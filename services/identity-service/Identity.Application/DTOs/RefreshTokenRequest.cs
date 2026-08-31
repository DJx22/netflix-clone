namespace Identity.Application.DTOs;

/// <summary>Maps to POST /api/v1/auth/refresh and POST /api/v1/auth/revoke (openapi.yaml RefreshRequest schema).</summary>
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
