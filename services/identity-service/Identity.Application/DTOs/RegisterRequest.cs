namespace Identity.Application.DTOs;

/// <summary>Maps to POST /api/v1/auth/register (openapi.yaml RegisterRequest schema).</summary>
public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
