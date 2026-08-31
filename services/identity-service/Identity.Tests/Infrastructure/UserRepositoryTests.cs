using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence.Repositories;

namespace Identity.Tests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="UserRepository"/> against a real SQL Server container.
/// §14: "Infrastructure gets integration tests against a real (containerised) database, not mocks."
///
/// Each test creates its own DbContext to avoid shared state between tests.
/// Tests clean up after themselves by operating on isolated data (unique emails).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UserRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── AddAsync / FindByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NewUser_CanBeFoundById()
    {
        var user = CreateUser("addbyid@example.com");
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);

        await repo.AddAsync(user);
        var found = await repo.FindByIdAsync(user.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
        found.Email.Value.Should().Be("addbyid@example.com");
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var repo   = new UserRepository(context);
        var result = await repo.FindByIdAsync(UserId.New());
        result.Should().BeNull();
    }

    // ── FindByEmailAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task FindByEmailAsync_KnownEmail_ReturnsUser()
    {
        var user = CreateUser("findbyemail@example.com");
        await using var addCtx = _fixture.CreateContext();
        await new UserRepository(addCtx).AddAsync(user);

        await using var findCtx = _fixture.CreateContext();
        var found = await new UserRepository(findCtx)
            .FindByEmailAsync(Email.From("findbyemail@example.com"));

        found.Should().NotBeNull();
        found!.Email.Value.Should().Be("findbyemail@example.com");
    }

    [Fact]
    public async Task FindByEmailAsync_UnknownEmail_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var result = await new UserRepository(context)
            .FindByEmailAsync(Email.From("ghost@example.com"));
        result.Should().BeNull();
    }

    // ── ExistsWithEmailAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ExistsWithEmailAsync_KnownEmail_ReturnsTrue()
    {
        var user = CreateUser("exists@example.com");
        await using var addCtx = _fixture.CreateContext();
        await new UserRepository(addCtx).AddAsync(user);

        await using var checkCtx = _fixture.CreateContext();
        var exists = await new UserRepository(checkCtx)
            .ExistsWithEmailAsync(Email.From("exists@example.com"));

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsWithEmailAsync_UnknownEmail_ReturnsFalse()
    {
        await using var context = _fixture.CreateContext();
        var exists = await new UserRepository(context)
            .ExistsWithEmailAsync(Email.From("nope@example.com"));
        exists.Should().BeFalse();
    }

    // ── UpdateAsync (refresh token persistence) ───────────────────────────────

    [Fact]
    public async Task UpdateAsync_AfterIssuingRefreshToken_PersistsToken()
    {
        var user = CreateUser("update-issue@example.com");
        await using var addCtx = _fixture.CreateContext();
        await new UserRepository(addCtx).AddAsync(user);

        // Load fresh, mutate, persist.
        await using var updateCtx = _fixture.CreateContext();
        var loaded = await new UserRepository(updateCtx)
            .FindByIdAsync(user.Id);
        loaded!.IssueRefreshToken("rt-abc", DateTime.UtcNow.AddDays(7));
        await new UserRepository(updateCtx).UpdateAsync(loaded);

        // Reload in a third context to verify the token was actually written.
        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new UserRepository(verifyCtx).FindByIdAsync(user.Id);
        verified!.RefreshTokens.Should().HaveCount(1);
        verified.RefreshTokens[0].TokenValue.Should().Be("rt-abc");
    }

    [Fact]
    public async Task UpdateAsync_AfterRevoking_PersistsIsRevokedTrue()
    {
        var user = CreateUser("update-revoke@example.com");
        await using var addCtx = _fixture.CreateContext();
        await new UserRepository(addCtx).AddAsync(user);

        await using var issueCtx = _fixture.CreateContext();
        var loaded = await new UserRepository(issueCtx).FindByIdAsync(user.Id);
        loaded!.IssueRefreshToken("rt-to-revoke", DateTime.UtcNow.AddDays(7));
        await new UserRepository(issueCtx).UpdateAsync(loaded);

        await using var revokeCtx = _fixture.CreateContext();
        var withToken = await new UserRepository(revokeCtx).FindByIdAsync(user.Id);
        withToken!.RevokeRefreshToken("rt-to-revoke");
        await new UserRepository(revokeCtx).UpdateAsync(withToken);

        await using var verifyCtx = _fixture.CreateContext();
        var verified = await new UserRepository(verifyCtx).FindByIdAsync(user.Id);
        verified!.RefreshTokens.Single().IsRevoked.Should().BeTrue();
    }

    // ── FindByRefreshTokenAsync ───────────────────────────────────────────────

    [Fact]
    public async Task FindByRefreshTokenAsync_KnownToken_ReturnsOwningUser()
    {
        var user = CreateUser("find-by-rt@example.com");
        await using var addCtx = _fixture.CreateContext();
        var addRepo = new UserRepository(addCtx);
        await addRepo.AddAsync(user);

        await using var issueCtx = _fixture.CreateContext();
        var loaded = await new UserRepository(issueCtx).FindByIdAsync(user.Id);
        loaded!.IssueRefreshToken("lookup-token", DateTime.UtcNow.AddDays(7));
        await new UserRepository(issueCtx).UpdateAsync(loaded);

        await using var findCtx = _fixture.CreateContext();
        var found = await new UserRepository(findCtx)
            .FindByRefreshTokenAsync("lookup-token");

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task FindByRefreshTokenAsync_UnknownToken_ReturnsNull()
    {
        await using var context = _fixture.CreateContext();
        var result = await new UserRepository(context)
            .FindByRefreshTokenAsync("ghost-token");
        result.Should().BeNull();
    }

    // ── Unique email constraint (database-enforced, domain rule) ─────────────

    [Fact]
    public async Task AddAsync_DuplicateEmail_ThrowsDatabaseException()
    {
        // The unique index on Users.Email (UserConfiguration) enforces the one-account-per-email rule.
        var first  = CreateUser("duplicate@example.com");
        var second = CreateUser("duplicate@example.com");

        await using var ctx1 = _fixture.CreateContext();
        await new UserRepository(ctx1).AddAsync(first);

        await using var ctx2 = _fixture.CreateContext();
        var act = async () => await new UserRepository(ctx2).AddAsync(second);

        // The specific exception type is an EF/SQL Server detail — we only assert that
        // the database rejects it; the exact exception type is an infrastructure concern.
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static User CreateUser(string email) =>
        User.Create(Email.From(email), PasswordHash.From("$2a$12$fakehashvalue"));
}
