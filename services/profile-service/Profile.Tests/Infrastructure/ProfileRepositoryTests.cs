using FluentAssertions;
using Profile.Domain.Entities;
using Profile.Domain.ValueObjects;
using Profile.Infrastructure.Persistence.Repositories;

namespace Profile.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="ProfileRepository"/> against a real SQL Server container.
/// §14: "Infrastructure gets integration tests against a real (containerised) database, not mocks."
///
/// Each test creates its own DbContext to avoid shared state between tests.
/// Tests clean up after themselves by operating on isolated data (unique account IDs).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProfileRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public ProfileRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── AddAsync / FindByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewProfile_CanBeFoundById()
    {
        var profile = CreateProfile();
        await using var context = _fixture.CreateContext();
        var repo = new ProfileRepository(context);

        await repo.AddAsync(profile);
        var found = await repo.FindByIdAsync(profile.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(profile.Id);
        found.DisplayName.Value.Should().Be("Test Profile");
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ProfileRepository(context);
        var result = await repo.FindByIdAsync(ProfileId.New());
        result.Should().BeNull();
    }

    // ── FindByAccountIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task FindByAccountIdAsync_HasProfiles_ReturnsAll()
    {
        var accountId = AccountId.From(Guid.NewGuid());
        var p1 = CreateProfile(accountId, "Profile 1");
        var p2 = CreateProfile(accountId, "Profile 2");

        await using var addCtx = _fixture.CreateContext();
        var addRepo = new ProfileRepository(addCtx);
        await addRepo.AddAsync(p1);
        await addRepo.AddAsync(p2);

        await using var findCtx = _fixture.CreateContext();
        var found = await new ProfileRepository(findCtx).FindByAccountIdAsync(accountId);

        found.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindByAccountIdAsync_NoProfiles_ReturnsEmptyList()
    {
        await using var context = _fixture.CreateContext();
        var result = await new ProfileRepository(context)
            .FindByAccountIdAsync(AccountId.From(Guid.NewGuid()));
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByAccountIdAsync_DoesNotReturnOtherAccountsProfiles()
    {
        var account1 = AccountId.From(Guid.NewGuid());
        var account2 = AccountId.From(Guid.NewGuid());

        await using var addCtx = _fixture.CreateContext();
        var addRepo = new ProfileRepository(addCtx);
        await addRepo.AddAsync(CreateProfile(account1, "Account1 Profile"));
        await addRepo.AddAsync(CreateProfile(account2, "Account2 Profile"));

        await using var findCtx = _fixture.CreateContext();
        var found = await new ProfileRepository(findCtx).FindByAccountIdAsync(account1);

        found.Should().HaveCount(1);
        found[0].DisplayName.Value.Should().Be("Account1 Profile");
    }

    // ── CountByAccountIdAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CountByAccountIdAsync_HasProfiles_ReturnsCorrectCount()
    {
        var accountId = AccountId.From(Guid.NewGuid());

        await using var addCtx = _fixture.CreateContext();
        var addRepo = new ProfileRepository(addCtx);
        await addRepo.AddAsync(CreateProfile(accountId, "Count P1"));
        await addRepo.AddAsync(CreateProfile(accountId, "Count P2"));
        await addRepo.AddAsync(CreateProfile(accountId, "Count P3"));

        await using var countCtx = _fixture.CreateContext();
        var count = await new ProfileRepository(countCtx).CountByAccountIdAsync(accountId);

        count.Should().Be(3);
    }

    [Fact]
    public async Task CountByAccountIdAsync_NoProfiles_ReturnsZero()
    {
        await using var context = _fixture.CreateContext();
        var count = await new ProfileRepository(context)
            .CountByAccountIdAsync(AccountId.From(Guid.NewGuid()));
        count.Should().Be(0);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AfterMutation_PersistsChanges()
    {
        var profile = CreateProfile();
        await using var addCtx = _fixture.CreateContext();
        await new ProfileRepository(addCtx).AddAsync(profile);

        // Load fresh, mutate, persist.
        await using var updateCtx = _fixture.CreateContext();
        var loaded = await new ProfileRepository(updateCtx).FindByIdAsync(profile.Id);
        loaded!.Update(
            loaded.AccountId,
            DisplayName.From("Updated Name"),
            "new-avatar",
            "fr",
            "R");
        await new ProfileRepository(updateCtx).UpdateAsync(loaded);

        // Reload in a third context to verify changes were actually written.
        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new ProfileRepository(verifyCtx).FindByIdAsync(profile.Id);
        verified!.DisplayName.Value.Should().Be("Updated Name");
        verified.AvatarId.Should().Be("new-avatar");
        verified.PreferredLanguage.Should().Be("fr");
        verified.MaturityRating.Should().Be("R");
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingProfile_RemovesFromDatabase()
    {
        var profile = CreateProfile();
        await using var addCtx = _fixture.CreateContext();
        await new ProfileRepository(addCtx).AddAsync(profile);

        await using var deleteCtx = _fixture.CreateContext();
        var loaded = await new ProfileRepository(deleteCtx).FindByIdAsync(profile.Id);
        loaded.Should().NotBeNull();
        await new ProfileRepository(deleteCtx).DeleteAsync(loaded!);

        await using var verifyCtx = _fixture.CreateContext();
        var deleted = await new ProfileRepository(verifyCtx).FindByIdAsync(profile.Id);
        deleted.Should().BeNull();
    }

    // ── Value object round-trip (EF Core conversions) ─────────────────────────

    [Fact]
    public async Task AddAsync_KidsProfile_PersistsIsKidsProfileTrue()
    {
        var profile = UserProfile.Create(
            AccountId.From(Guid.NewGuid()),
            DisplayName.From("Kids"),
            null,
            isKidsProfile: true);

        await using var addCtx = _fixture.CreateContext();
        await new ProfileRepository(addCtx).AddAsync(profile);

        await using var findCtx = _fixture.CreateContext();
        var found = await new ProfileRepository(findCtx).FindByIdAsync(profile.Id);
        found!.IsKidsProfile.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_NullOptionalFields_PersistsAsNull()
    {
        var profile = CreateProfile();

        await using var addCtx = _fixture.CreateContext();
        await new ProfileRepository(addCtx).AddAsync(profile);

        await using var findCtx = _fixture.CreateContext();
        var found = await new ProfileRepository(findCtx).FindByIdAsync(profile.Id);
        found!.AvatarId.Should().BeNull();
        found.PreferredLanguage.Should().BeNull();
        found.MaturityRating.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserProfile CreateProfile(
        AccountId? accountId = null,
        string displayName = "Test Profile") =>
        UserProfile.Create(
            accountId ?? AccountId.From(Guid.NewGuid()),
            DisplayName.From(displayName),
            avatarId: null);
}
