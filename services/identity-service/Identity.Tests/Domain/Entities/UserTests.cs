using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Exceptions;
using Identity.Domain.ValueObjects;

namespace Identity.Tests.Domain.Entities;

/// <summary>
/// Tests every invariant on the User aggregate root.
/// All tests use the domain's own factory methods — no reflection or back-door construction.
/// </summary>
public sealed class UserTests
{
    private static readonly Email   ValidEmail    = Email.From("user@example.com");
    private static readonly PasswordHash ValidHash = PasswordHash.From("$2a$12$fakehashvalue");

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidArguments_ReturnsUserWithNonEmptyId()
    {
        var user = User.Create(ValidEmail, ValidHash);
        user.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ValidArguments_SetsEmailAndHash()
    {
        var user = User.Create(ValidEmail, ValidHash);
        user.Email.Should().Be(ValidEmail);
        user.PasswordHash.Value.Should().Be(ValidHash.Value);
    }

    [Fact]
    public void Create_ValidArguments_SetsCreatedAtUtcToNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var user   = User.Create(ValidEmail, ValidHash);
        var after  = DateTime.UtcNow.AddSeconds(1);

        user.CreatedAtUtc.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void Create_NewUser_HasNoRefreshTokens()
    {
        var user = User.Create(ValidEmail, ValidHash);
        user.RefreshTokens.Should().BeEmpty();
    }

    // ── IssueRefreshToken ────────────────────────────────────────────────────

    [Fact]
    public void IssueRefreshToken_ValidArguments_AddsTokenToCollection()
    {
        var user    = User.Create(ValidEmail, ValidHash);
        var expires = DateTime.UtcNow.AddDays(7);

        user.IssueRefreshToken("token-value-abc", expires);

        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens[0].TokenValue.Should().Be("token-value-abc");
    }

    [Fact]
    public void IssueRefreshToken_ValidArguments_TokenIsNotRevoked()
    {
        var user = User.Create(ValidEmail, ValidHash);
        var token = user.IssueRefreshToken("tok", DateTime.UtcNow.AddDays(1));
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IssueRefreshToken_ValidArguments_TokenBelongsToUser()
    {
        var user  = User.Create(ValidEmail, ValidHash);
        var token = user.IssueRefreshToken("tok", DateTime.UtcNow.AddDays(1));
        token.UserId.Should().Be(user.Id);
    }

    // ── RotateRefreshToken (single-use invariant, openapi.yaml lines 91-93) ─

    [Fact]
    public void RotateRefreshToken_ValidToken_RevokesOldAndReturnsNew()
    {
        var user    = User.Create(ValidEmail, ValidHash);
        var expires = DateTime.UtcNow.AddDays(7);
        user.IssueRefreshToken("old-token", expires);

        var newToken = user.RotateRefreshToken("old-token", "new-token", expires);

        var old = user.RefreshTokens.Single(t => t.TokenValue == "old-token");
        old.IsRevoked.Should().BeTrue();
        newToken.TokenValue.Should().Be("new-token");
        newToken.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void RotateRefreshToken_AlreadyRevokedToken_ThrowsInvalidRefreshTokenException()
    {
        // Single-use: a token that was already rotated/revoked cannot be used again.
        var user    = User.Create(ValidEmail, ValidHash);
        var expires = DateTime.UtcNow.AddDays(7);
        user.IssueRefreshToken("old-token", expires);
        user.RotateRefreshToken("old-token", "new-token", expires);

        // Attempting to rotate the already-revoked "old-token" again.
        var act = () => user.RotateRefreshToken("old-token", "another-token", expires);
        act.Should().Throw<InvalidRefreshTokenException>();
    }

    [Fact]
    public void RotateRefreshToken_ExpiredToken_ThrowsInvalidRefreshTokenException()
    {
        // Expired tokens must be rejected on /refresh (openapi.yaml lines 110-115).
        var user    = User.Create(ValidEmail, ValidHash);
        var alreadyExpired = DateTime.UtcNow.AddDays(-1);

        // Reconstitute bypasses the Create invariant so we can inject an expired token.
        var expiredToken = RefreshToken.Reconstitute(
            Guid.NewGuid(), "expired-tok", alreadyExpired, DateTime.UtcNow.AddDays(-8),
            isRevoked: false, user.Id);

        var userWithExpired = User.Reconstitute(
            user.Id, user.Email, user.PasswordHash, user.CreatedAtUtc,
            new[] { expiredToken });

        var act = () => userWithExpired.RotateRefreshToken(
            "expired-tok", "new-tok", DateTime.UtcNow.AddDays(7));
        act.Should().Throw<InvalidRefreshTokenException>();
    }

    [Fact]
    public void RotateRefreshToken_UnknownToken_ThrowsInvalidRefreshTokenException()
    {
        var user = User.Create(ValidEmail, ValidHash);
        var act  = () => user.RotateRefreshToken("ghost-token", "new-token", DateTime.UtcNow.AddDays(7));
        act.Should().Throw<InvalidRefreshTokenException>();
    }

    // ── RevokeRefreshToken (logout invariants, openapi.yaml lines 120-124) ──

    [Fact]
    public void RevokeRefreshToken_ValidToken_SetsIsRevokedTrue()
    {
        var user = User.Create(ValidEmail, ValidHash);
        user.IssueRefreshToken("logout-tok", DateTime.UtcNow.AddDays(7));

        user.RevokeRefreshToken("logout-tok");

        user.RefreshTokens.Single().IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void RevokeRefreshToken_ExpiredButNotRevokedToken_Succeeds()
    {
        // Spec (openapi.yaml lines 120-124): logout must work even after the session expired.
        // RevokeRefreshToken must NOT throw for an expired-but-not-revoked token.
        var user = User.Create(ValidEmail, ValidHash);
        var expiredToken = RefreshToken.Reconstitute(
            Guid.NewGuid(), "expired-tok",
            expiresAtUtc:  DateTime.UtcNow.AddDays(-1),
            createdAtUtc:  DateTime.UtcNow.AddDays(-8),
            isRevoked: false, user.Id);

        var userWithExpired = User.Reconstitute(
            user.Id, user.Email, user.PasswordHash, user.CreatedAtUtc,
            new[] { expiredToken });

        var act = () => userWithExpired.RevokeRefreshToken("expired-tok");
        act.Should().NotThrow();
    }

    [Fact]
    public void RevokeRefreshToken_AlreadyRevokedToken_ThrowsInvalidRefreshTokenException()
    {
        // Revoking an already-revoked token should fail (openapi.yaml lines 137-142).
        var user = User.Create(ValidEmail, ValidHash);
        user.IssueRefreshToken("tok", DateTime.UtcNow.AddDays(7));
        user.RevokeRefreshToken("tok");

        var act = () => user.RevokeRefreshToken("tok");
        act.Should().Throw<InvalidRefreshTokenException>();
    }

    [Fact]
    public void RevokeRefreshToken_UnknownToken_ThrowsInvalidRefreshTokenException()
    {
        var user = User.Create(ValidEmail, ValidHash);
        var act  = () => user.RevokeRefreshToken("ghost-token");
        act.Should().Throw<InvalidRefreshTokenException>();
    }
}
