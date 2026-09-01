using FluentAssertions;
using Profile.Domain.Entities;
using Profile.Domain.Exceptions;
using Profile.Domain.ValueObjects;

namespace Profile.Tests.Domain.Entities;

/// <summary>
/// Tests every invariant on the UserProfile aggregate root.
/// All tests use the domain's own factory methods — no reflection or back-door construction.
/// </summary>
public sealed class UserProfileTests
{
    private static readonly AccountId   TestAccountId   = AccountId.From(Guid.NewGuid());
    private static readonly DisplayName TestDisplayName = DisplayName.From("Alice");

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidArguments_ReturnsProfileWithNonEmptyId()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        profile.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ValidArguments_SetsAccountIdAndDisplayName()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, "avatar-1");
        profile.AccountId.Should().Be(TestAccountId);
        profile.DisplayName.Should().Be(TestDisplayName);
        profile.AvatarId.Should().Be("avatar-1");
    }

    [Fact]
    public void Create_ValidArguments_SetsCreatedAtUtcToNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        var after = DateTime.UtcNow.AddSeconds(1);

        profile.CreatedAtUtc.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void Create_DefaultIsKidsProfile_IsFalse()
    {
        // openapi.yaml CreateProfileRequest.isKidsProfile default: false.
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        profile.IsKidsProfile.Should().BeFalse();
    }

    [Fact]
    public void Create_IsKidsProfileTrue_SetsFlag()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null, isKidsProfile: true);
        profile.IsKidsProfile.Should().BeTrue();
    }

    [Fact]
    public void Create_NullPreferredLanguageAndMaturityRating_AreNull()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        profile.PreferredLanguage.Should().BeNull();
        profile.MaturityRating.Should().BeNull();
    }

    [Fact]
    public void Create_WhitespaceAvatarId_NormalizesToNull()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, "   ");
        profile.AvatarId.Should().BeNull();
    }

    // ── EnsureOwnership ──────────────────────────────────────────────────────

    [Fact]
    public void EnsureOwnership_SameAccount_DoesNotThrow()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        var act = () => profile.EnsureOwnership(TestAccountId);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureOwnership_DifferentAccount_ThrowsProfileNotOwnedException()
    {
        // openapi.yaml: 403 when profile belongs to a different account.
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        var otherAccount = AccountId.From(Guid.NewGuid());

        var act = () => profile.EnsureOwnership(otherAccount);
        act.Should().Throw<ProfileNotOwnedException>();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ValidOwner_UpdatesMutableFields()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        var newName = DisplayName.From("Bob");

        profile.Update(TestAccountId, newName, "avatar-2", "en", "PG-13");

        profile.DisplayName.Should().Be(newName);
        profile.AvatarId.Should().Be("avatar-2");
        profile.PreferredLanguage.Should().Be("en");
        profile.MaturityRating.Should().Be("PG-13");
    }

    [Fact]
    public void Update_DifferentAccount_ThrowsProfileNotOwnedException()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null);
        var otherAccount = AccountId.From(Guid.NewGuid());

        var act = () => profile.Update(otherAccount, TestDisplayName, null, null, null);
        act.Should().Throw<ProfileNotOwnedException>();
    }

    [Fact]
    public void Update_WhitespaceOptionalFields_NormalizesToNull()
    {
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, "avatar-1");

        profile.Update(TestAccountId, TestDisplayName, "  ", "  ", "  ");

        profile.AvatarId.Should().BeNull();
        profile.PreferredLanguage.Should().BeNull();
        profile.MaturityRating.Should().BeNull();
    }

    [Fact]
    public void Update_DoesNotChangeIsKidsProfile()
    {
        // isKidsProfile is not in UpdateProfileRequest — treated as immutable after creation.
        var profile = UserProfile.Create(TestAccountId, TestDisplayName, null, isKidsProfile: true);

        profile.Update(TestAccountId, DisplayName.From("New Name"), null, null, null);

        profile.IsKidsProfile.Should().BeTrue();
    }

    // ── Reconstitute ─────────────────────────────────────────────────────────

    [Fact]
    public void Reconstitute_AllFields_RestoresEntityState()
    {
        var id = ProfileId.New();
        var created = DateTime.UtcNow.AddDays(-10);

        var profile = UserProfile.Reconstitute(
            id, TestAccountId, TestDisplayName, "avatar-1",
            isKidsProfile: true, "es", "R", created);

        profile.Id.Should().Be(id);
        profile.AccountId.Should().Be(TestAccountId);
        profile.DisplayName.Should().Be(TestDisplayName);
        profile.AvatarId.Should().Be("avatar-1");
        profile.IsKidsProfile.Should().BeTrue();
        profile.PreferredLanguage.Should().Be("es");
        profile.MaturityRating.Should().Be("R");
        profile.CreatedAtUtc.Should().Be(created);
    }
}
