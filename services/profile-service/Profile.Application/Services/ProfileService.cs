using Profile.Application.DTOs;
using Profile.Application.Exceptions;
using Profile.Domain.Entities;
using Profile.Domain.Repositories;
using Profile.Domain.ValueObjects;

namespace Profile.Application.Services;

/// <summary>
/// Orchestrates all profile use cases: list, get, create, update, delete.
/// Contains no business rules — those live in the domain. This class only
/// coordinates domain objects, calls repositories, and maps results to DTOs.
///
/// Plain application service rather than Mediator/CQRS: reads and writes share the
/// same shape (ProfileResponse), and operations are simple CRUD through one aggregate.
/// §6 reserves Mediator for meaningfully different read/write shapes — that does not
/// apply here.
/// </summary>
public sealed class ProfileService
{
    private readonly IProfileRepository _profileRepository;

    public ProfileService(IProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    /// <summary>
    /// Lists all profiles belonging to the caller's account.
    /// </summary>
    public async Task<IReadOnlyList<ProfileResponse>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var typedAccountId = AccountId.From(accountId);

        var profiles = await _profileRepository
            .FindByAccountIdAsync(typedAccountId, cancellationToken)
            .ConfigureAwait(false);

        return profiles.Select(MapToProfileResponse).ToList();
    }

    /// <summary>
    /// Returns a single profile. Validates ownership against the caller's account.
    /// </summary>
    /// <exception cref="ProfileNotFoundException">Thrown when the profile ID has no matching row.</exception>
    /// <exception cref="Domain.Exceptions.ProfileNotOwnedException">Thrown when the profile belongs to a different account.</exception>
    public async Task<ProfileResponse> GetByIdAsync(
        Guid profileId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileOrThrowAsync(profileId, cancellationToken)
            .ConfigureAwait(false);

        profile.EnsureOwnership(AccountId.From(accountId));

        return MapToProfileResponse(profile);
    }

    /// <summary>
    /// Creates a new profile under the caller's account.
    /// Enforces the per-account profile limit (5) — the spec explicitly places this
    /// rule in Application, not in Domain or a database constraint (openapi.yaml lines 40-42).
    /// </summary>
    /// <exception cref="ProfileLimitReachedException">Thrown when the account already has 5 profiles.</exception>
    public async Task<ProfileResponse> CreateAsync(
        Guid accountId,
        CreateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var typedAccountId = AccountId.From(accountId);

        var currentCount = await _profileRepository
            .CountByAccountIdAsync(typedAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (currentCount >= ProfileLimitReachedException.MaxProfilesPerAccount)
        {
            throw new ProfileLimitReachedException();
        }

        var displayName = DisplayName.From(request.DisplayName);

        var profile = UserProfile.Create(
            typedAccountId,
            displayName,
            request.AvatarId,
            request.IsKidsProfile);

        await _profileRepository.AddAsync(profile, cancellationToken).ConfigureAwait(false);

        return MapToProfileResponse(profile);
    }

    /// <summary>
    /// Updates a profile's mutable fields. Ownership is validated by the domain entity.
    /// </summary>
    /// <exception cref="ProfileNotFoundException">Thrown when the profile ID has no matching row.</exception>
    /// <exception cref="Domain.Exceptions.ProfileNotOwnedException">Thrown when the profile belongs to a different account.</exception>
    public async Task<ProfileResponse> UpdateAsync(
        Guid profileId,
        Guid accountId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileOrThrowAsync(profileId, cancellationToken)
            .ConfigureAwait(false);

        var callerAccountId = AccountId.From(accountId);
        var displayName = DisplayName.From(request.DisplayName);

        profile.Update(
            callerAccountId,
            displayName,
            request.AvatarId,
            request.PreferredLanguage,
            request.MaturityRating);

        await _profileRepository.UpdateAsync(profile, cancellationToken).ConfigureAwait(false);

        return MapToProfileResponse(profile);
    }

    /// <summary>
    /// Deletes a profile. Validates ownership before removal.
    /// </summary>
    /// <exception cref="ProfileNotFoundException">Thrown when the profile ID has no matching row.</exception>
    /// <exception cref="Domain.Exceptions.ProfileNotOwnedException">Thrown when the profile belongs to a different account.</exception>
    public async Task DeleteAsync(
        Guid profileId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileOrThrowAsync(profileId, cancellationToken)
            .ConfigureAwait(false);

        profile.EnsureOwnership(AccountId.From(accountId));

        await _profileRepository.DeleteAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserProfile> FindProfileOrThrowAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var typedId = ProfileId.From(profileId);

        var profile = await _profileRepository
            .FindByIdAsync(typedId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            throw new ProfileNotFoundException(profileId);
        }

        return profile;
    }

    private static ProfileResponse MapToProfileResponse(UserProfile profile)
    {
        return new ProfileResponse
        {
            ProfileId = profile.Id.Value,
            AccountId = profile.AccountId.Value,
            DisplayName = profile.DisplayName.Value,
            AvatarId = profile.AvatarId,
            IsKidsProfile = profile.IsKidsProfile,
            PreferredLanguage = profile.PreferredLanguage,
            MaturityRating = profile.MaturityRating,
            CreatedAtUtc = profile.CreatedAtUtc
        };
    }
}
