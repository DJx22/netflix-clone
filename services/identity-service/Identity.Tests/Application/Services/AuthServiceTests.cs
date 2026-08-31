using FluentAssertions;
using Identity.Application.DTOs;
using Identity.Application.Exceptions;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Exceptions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using Moq;

namespace Identity.Tests.Application.Services;

/// <summary>
/// Unit tests for AuthService. All dependencies are mocked — no database, no real hashing,
/// no real JWT signing. The point is to verify orchestration logic only.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repoMock    = new();
    private readonly Mock<IPasswordHasher> _hasherMock  = new();
    private readonly Mock<ITokenService>   _tokensMock  = new();
    private readonly AuthService           _service;

    private static readonly Email        TestEmail    = Email.From("user@example.com");
    private static readonly PasswordHash TestHash     = PasswordHash.From("$2a$12$fakehash");
    private static readonly string       AccessToken  = "access.token.value";
    private static readonly DateTime     AccessExpiry = DateTime.UtcNow.AddMinutes(15);
    private static readonly string       RefreshTok   = "refresh-token-value";
    private static readonly DateTime     RefreshExp   = DateTime.UtcNow.AddDays(7);

    public AuthServiceTests()
    {
        _tokensMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns((AccessToken, AccessExpiry));

        _tokensMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns((RefreshTok, RefreshExp));

        _service = new AuthService(_repoMock.Object, _hasherMock.Object, _tokensMock.Object);
    }

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsUserResponse()
    {
        _repoMock.Setup(r => r.ExistsWithEmailAsync(TestEmail, default)).ReturnsAsync(false);
        _hasherMock.Setup(h => h.Hash(It.IsAny<Password>())).Returns(TestHash);

        var result = await _service.RegisterAsync(
            new RegisterRequest { Email = "user@example.com", Password = "password123", ConfirmPassword = "password123" });

        result.Email.Should().Be("user@example.com");
        result.UserId.Should().NotBeEmpty();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ThrowsEmailAlreadyRegisteredException()
    {
        // Spec: 409 when email already registered (openapi.yaml lines 50-55).
        _repoMock.Setup(r => r.ExistsWithEmailAsync(TestEmail, default)).ReturnsAsync(true);

        var act = async () => await _service.RegisterAsync(
            new RegisterRequest { Email = "user@example.com", Password = "password123", ConfirmPassword = "password123" });

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenResponse()
    {
        var user = User.Create(TestEmail, TestHash);
        _repoMock.Setup(r => r.FindByEmailAsync(TestEmail, default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<Password>(), TestHash)).Returns(true);

        var result = await _service.LoginAsync(
            new LoginRequest { Email = "user@example.com", Password = "password123" });

        result.AccessToken.Should().Be(AccessToken);
        result.RefreshToken.Should().Be(RefreshTok);
        result.ExpiresAtUtc.Should().Be(AccessExpiry);
        _repoMock.Verify(r => r.UpdateAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsInvalidCredentialsException()
    {
        // Generic 401 — must not distinguish "email not found" from "wrong password" (openapi.yaml line 80).
        _repoMock.Setup(r => r.FindByEmailAsync(TestEmail, default)).ReturnsAsync((User?)null);

        var act = async () => await _service.LoginAsync(
            new LoginRequest { Email = "user@example.com", Password = "password123" });

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = User.Create(TestEmail, TestHash);
        _repoMock.Setup(r => r.FindByEmailAsync(TestEmail, default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<Password>(), TestHash)).Returns(false);

        // Password must be ≥10 chars and contain a digit to pass Password.From(),
        // so the mock hasher is actually reached and can return false.
        var act = async () => await _service.LoginAsync(
            new LoginRequest { Email = "user@example.com", Password = "wrongpass1" });

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    // ── RefreshToken ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokenPair()
    {
        var user = User.Create(TestEmail, TestHash);
        user.IssueRefreshToken(RefreshTok, RefreshExp);

        _repoMock.Setup(r => r.FindByRefreshTokenAsync(RefreshTok, default)).ReturnsAsync(user);

        var result = await _service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = RefreshTok });

        result.AccessToken.Should().Be(AccessToken);
        result.RefreshToken.Should().Be(RefreshTok); // mock always returns same value
        _repoMock.Verify(r => r.UpdateAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_UnknownToken_ThrowsInvalidRefreshTokenException()
    {
        // openapi.yaml lines 110-115: 401 for invalid/expired/revoked tokens.
        _repoMock.Setup(r => r.FindByRefreshTokenAsync("ghost", default)).ReturnsAsync((User?)null);

        var act = async () => await _service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = "ghost" });

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    // ── RevokeToken ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeTokenAsync_ValidToken_RevokesAndPersists()
    {
        var user = User.Create(TestEmail, TestHash);
        user.IssueRefreshToken(RefreshTok, RefreshExp);

        _repoMock.Setup(r => r.FindByRefreshTokenAsync(RefreshTok, default)).ReturnsAsync(user);

        await _service.RevokeTokenAsync(new RefreshTokenRequest { RefreshToken = RefreshTok });

        user.RefreshTokens.Single().IsRevoked.Should().BeTrue();
        _repoMock.Verify(r => r.UpdateAsync(user, default), Times.Once);
    }

    [Fact]
    public async Task RevokeTokenAsync_UnknownToken_ThrowsInvalidRefreshTokenException()
    {
        // openapi.yaml lines 137-142: 401 for invalid/revoked tokens.
        _repoMock.Setup(r => r.FindByRefreshTokenAsync("ghost", default)).ReturnsAsync((User?)null);

        var act = async () => await _service.RevokeTokenAsync(
            new RefreshTokenRequest { RefreshToken = "ghost" });

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    // ── GetCurrentUser ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUserAsync_KnownId_ReturnsUserResponse()
    {
        var user   = User.Create(TestEmail, TestHash);
        var userId = user.Id.Value;

        _repoMock.Setup(r => r.FindByIdAsync(UserId.From(userId), default)).ReturnsAsync(user);

        var result = await _service.GetCurrentUserAsync(userId);

        result.UserId.Should().Be(userId);
        result.Email.Should().Be(TestEmail.Value);
    }

    [Fact]
    public async Task GetCurrentUserAsync_UnknownId_ThrowsUserNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.FindByIdAsync(UserId.From(unknownId), default))
            .ReturnsAsync((User?)null);

        var act = async () => await _service.GetCurrentUserAsync(unknownId);
        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
