namespace Profile.Application.DTOs;

/// <summary>
/// Maps to openapi.yaml ProfileResponse schema.
/// Returned by list, get, create, and update. Never exposes the domain entity directly.
/// </summary>
public sealed class ProfileResponse
{
    public Guid ProfileId { get; init; }
    public Guid AccountId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarId { get; init; }
    public bool IsKidsProfile { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? MaturityRating { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
