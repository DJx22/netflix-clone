namespace Identity.Application.DTOs;

/// <summary>
/// Maps to openapi.yaml UserResponse schema.
/// Returned by /register and /me. Never exposes the domain entity or password hash.
/// </summary>
public sealed class UserResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
