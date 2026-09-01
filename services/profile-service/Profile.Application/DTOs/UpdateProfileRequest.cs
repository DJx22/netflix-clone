namespace Profile.Application.DTOs;

/// <summary>Maps to PUT /api/v1/profiles/{profileId} (openapi.yaml UpdateProfileRequest schema).</summary>
public sealed class UpdateProfileRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarId { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? MaturityRating { get; init; }
}
