using FluentAssertions;
using Profile.Application.DTOs;
using Profile.Application.Exceptions;
using Profile.Application.Services;
using Profile.Domain.Entities;
using Profile.Domain.Exceptions;
using Profile.Domain.Repositories;
using Profile.Domain.ValueObjects;
using Moq;

namespace Profile.Tests.Application.Services;

/// <summary>
/// Unit tests for ProfileService. All dependencies are mocked — no database.
/// The point is to verify orchestration logic only.
/// </summary>
public sealed class ProfileServiceTests
{
    private readonly Mock<IProfileRepository> _repoMock = new();
    private readonly ProfileService _service;

    private static readonly Guid TestAccountGuid = Guid.NewGuid();
    private static readonly AccountId TestAccountId = AccountId.From(TestAccountGuid);
    private static readonly DisplayName TestDisplayName = DisplayName.From("Alice");

    public ProfileServiceTests()
    {
        _service = new ProfileService(_repoMock.Object);
    }

    // ── ListByAccountAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ListByAccountAsync_HasProfiles_ReturnsMappedResponses()
    {
        var profiles = new List<UserProfile>
        {
            UserProfile.Create(TestAccountId, DisplayName.From("Profile 1"), null),
            UserProfile.Create(TestAccountId, DisplayName.From("Profile 2"), null)
        };
        _repoMock.Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(profiles);

        var result = await _service.ListByAccountAsync(TestAccountGuid);

        result.Should().HaveCount(2);
        result[0].DisplayName.Should().Be("Profile 1");
        result[1].DisplayName.Should().Be("Profile 2");
    }

    [Fact]
    public async Task ListByAccountAsync_NoProfiles_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.FindByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(new List<UserProfile>());

        var result = await _service.ListByAccountAsync(TestAccountGuid);

        result.Should().BeEmpty();
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingProfileOwnedByCaller_ReturnsResponse()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, "avatar-1");
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);

        var result = await _service.GetByIdAsync(profile.Id.Value, TestAccountGuid);

        result.ProfileId.Should().Be(profile.Id.Value);
        result.DisplayName.Should().Be("Alice");
        result.AvatarId.Should().Be("avatar-1");
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ThrowsProfileNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _repoMock.Setup(r => r.FindByIdAsync(ProfileId.From(unknownId), default))
                 .ReturnsAsync((UserProfile?)null);

        var act = async () => await _service.GetByIdAsync(unknownId, TestAccountGuid);

        await act.Should().ThrowAsync<ProfileNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_DifferentAccount_ThrowsProfileNotOwnedException()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);
        var otherAccount = Guid.NewGuid();

        var act = async () => await _service.GetByIdAsync(profile.Id.Value, otherAccount);

        await act.Should().ThrowAsync<ProfileNotOwnedException>();
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_UnderLimit_ReturnsCreatedProfile()
    {
        _repoMock.Setup(r => r.CountByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(0);

        var request = new CreateProfileRequest { DisplayName = "New Profile" };
        var result = await _service.CreateAsync(TestAccountGuid, request);

        result.DisplayName.Should().Be("New Profile");
        result.AccountId.Should().Be(TestAccountGuid);
        result.IsKidsProfile.Should().BeFalse();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UserProfile>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_IsKidsProfileTrue_ReturnsProfileWithFlagSet()
    {
        _repoMock.Setup(r => r.CountByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(0);

        var request = new CreateProfileRequest { DisplayName = "Kids", IsKidsProfile = true };
        var result = await _service.CreateAsync(TestAccountGuid, request);

        result.IsKidsProfile.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_AtLimit_ThrowsProfileLimitReachedException()
    {
        // openapi.yaml lines 40-42: max 5 profiles per account.
        _repoMock.Setup(r => r.CountByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(ProfileLimitReachedException.MaxProfilesPerAccount);

        var request = new CreateProfileRequest { DisplayName = "Too Many" };
        var act = async () => await _service.CreateAsync(TestAccountGuid, request);

        await act.Should().ThrowAsync<ProfileLimitReachedException>();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UserProfile>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ExactlyAtLimitMinus1_Succeeds()
    {
        // Boundary: 4 existing profiles → 5th should succeed.
        _repoMock.Setup(r => r.CountByAccountIdAsync(TestAccountId, default))
                 .ReturnsAsync(ProfileLimitReachedException.MaxProfilesPerAccount - 1);

        var request = new CreateProfileRequest { DisplayName = "Fifth Profile" };
        var result = await _service.CreateAsync(TestAccountGuid, request);

        result.DisplayName.Should().Be("Fifth Profile");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidOwner_UpdatesAndReturnsResponse()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);

        var request = new UpdateProfileRequest
        {
            DisplayName = "Updated Name",
            AvatarId = "avatar-2",
            PreferredLanguage = "en",
            MaturityRating = "PG-13"
        };

        var result = await _service.UpdateAsync(profile.Id.Value, TestAccountGuid, request);

        result.DisplayName.Should().Be("Updated Name");
        result.AvatarId.Should().Be("avatar-2");
        result.PreferredLanguage.Should().Be("en");
        result.MaturityRating.Should().Be("PG-13");
        _repoMock.Verify(r => r.UpdateAsync(profile, default), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ThrowsProfileNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _repoMock.Setup(r => r.FindByIdAsync(ProfileId.From(unknownId), default))
                 .ReturnsAsync((UserProfile?)null);

        var act = async () => await _service.UpdateAsync(
            unknownId, TestAccountGuid, new UpdateProfileRequest { DisplayName = "X" });

        await act.Should().ThrowAsync<ProfileNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_DifferentAccount_ThrowsProfileNotOwnedException()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);

        var act = async () => await _service.UpdateAsync(
            profile.Id.Value, Guid.NewGuid(), new UpdateProfileRequest { DisplayName = "X" });

        await act.Should().ThrowAsync<ProfileNotOwnedException>();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ValidOwner_DeletesProfile()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);

        await _service.DeleteAsync(profile.Id.Value, TestAccountGuid);

        _repoMock.Verify(r => r.DeleteAsync(profile, default), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsProfileNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _repoMock.Setup(r => r.FindByIdAsync(ProfileId.From(unknownId), default))
                 .ReturnsAsync((UserProfile?)null);

        var act = async () => await _service.DeleteAsync(unknownId, TestAccountGuid);

        await act.Should().ThrowAsync<ProfileNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_DifferentAccount_ThrowsProfileNotOwnedException()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        _repoMock.Setup(r => r.FindByIdAsync(profile.Id, default)).ReturnsAsync(profile);

        var act = async () => await _service.DeleteAsync(profile.Id.Value, Guid.NewGuid());

        await act.Should().ThrowAsync<ProfileNotOwnedException>();
    }
}
