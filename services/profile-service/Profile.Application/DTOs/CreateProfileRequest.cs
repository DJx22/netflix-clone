namespace Profile.Application.DTOs;

/// <summary>Maps to POST /api/v1/profiles (openapi.yaml CreateProfileRequest schema).</summary>
public sealed class CreateProfileRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarId { get; init; }
    public bool IsKidsProfile { get; init; }
}
