namespace Identity.Application.DTOs;

/// <summary>Maps to POST /api/v1/auth/login (openapi.yaml LoginRequest schema).</summary>
public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
