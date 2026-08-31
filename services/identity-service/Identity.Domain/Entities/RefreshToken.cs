using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

/// <summary>
/// Child entity owned exclusively by <see cref="User"/>.
/// Not an aggregate root — it has no meaning outside the owning User.
///
/// Three invariants come directly from the spec (openapi.yaml lines 91-93, 110-115, 120-124):
///   1. Single-use: a token is revoked the moment it is used to issue a new one.
///   2. Rejected if expired OR revoked on /refresh.
///   3. Accepted for revocation even after expiry (logout must work with a dead session).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; }

    /// <summary>The opaque token string issued to the client.</summary>
    public string TokenValue { get; }

    public DateTime ExpiresAtUtc { get; }

    public DateTime CreatedAtUtc { get; }

    /// <summary>
    /// Set to true once the token has been used for rotation or explicitly revoked.
    /// Using the question form per §3 of the coding standard.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>The user this token belongs to; stored for repository queries.</summary>
    public UserId UserId { get; }

    /// <summary>True when the token's validity window has closed.</summary>
    public bool HasExpired => DateTime.UtcNow >= ExpiresAtUtc;

    private RefreshToken(Guid id, string tokenValue, DateTime expiresAtUtc, DateTime createdAtUtc, UserId userId)
    {
        Id = id;
        TokenValue = tokenValue;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        UserId = userId;
    }

    internal static RefreshToken Create(string tokenValue, DateTime expiresAtUtc, UserId userId)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            throw new ArgumentException("Refresh token value cannot be empty.", nameof(tokenValue));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Refresh token expiry must be in the future.", nameof(expiresAtUtc));
        }

        return new RefreshToken(Guid.NewGuid(), tokenValue, expiresAtUtc, DateTime.UtcNow, userId);
    }

    /// <summary>
    /// Reconstitutes a <see cref="RefreshToken"/> from persisted state (repository use only).
    /// </summary>
    public static RefreshToken Reconstitute(
        Guid id,
        string tokenValue,
        DateTime expiresAtUtc,
        DateTime createdAtUtc,
        bool isRevoked,
        UserId userId)
    {
        var token = new RefreshToken(id, tokenValue, expiresAtUtc, createdAtUtc, userId);
        token.IsRevoked = isRevoked;
        return token;
    }

    /// <summary>Marks the token as consumed. Called by <see cref="User"/> only.</summary>
    internal void Revoke()
    {
        IsRevoked = true;
    }
}
