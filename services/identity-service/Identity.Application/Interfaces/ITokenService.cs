using Identity.Domain.Entities;

namespace Identity.Application.Interfaces;

/// <summary>
/// Produces JWT access tokens and cryptographically random refresh token strings.
/// Signing algorithm, key material, and TTL configuration are Infrastructure concerns;
/// Application only cares about the contract.
///
/// Separation note: refresh token *persistence* is handled by IUserRepository.
/// This interface is responsible only for *generating* the values.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed JWT for the given user.
    /// Returns the token string and its UTC expiry so the caller can build a <see cref="DTOs.TokenResponse"/>.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(User user);

    /// <summary>
    /// Generates a cryptographically random, opaque refresh token value.
    /// The TTL is determined by Infrastructure configuration; the returned expiry
    /// is what gets stored alongside the token in the database.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken();
}
