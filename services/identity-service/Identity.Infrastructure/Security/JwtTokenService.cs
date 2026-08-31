using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Produces signed JWT access tokens and cryptographically random refresh token strings.
///
/// Registered as Singleton: the only mutable state is the cached <see cref="SymmetricSecurityKey"/>,
/// which is derived once from the configured secret and is immutable thereafter.
/// <see cref="IOptions{T}"/> is safe for Singleton use — it resolves once at startup (§7).
///
/// Refresh token generation uses <see cref="RandomNumberGenerator"/> (CSPRNG) —
/// never <see cref="Random"/>.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private const int RefreshTokenByteLength = 64;

    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        // Fail fast at startup rather than issuing tokens with a weak or empty secret.
        if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret must be at least 32 characters. " +
                "Set it via environment variable or secret store — never in source.");
        }

        _signingKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(_options.Secret));
    }

    /// <inheritdoc/>
    public (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(User user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes);

        var claims = new[]
        {
            // sub is the standard subject claim — the user's identity.
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            // jti provides uniqueness per token for replay detection.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);

        return (handler.WriteToken(token), expiresAtUtc);
    }

    /// <inheritdoc/>
    public (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenExpiryDays);

        // Base64Url-encode 64 random bytes → 86 URL-safe characters with no padding.
        Span<byte> bytes = stackalloc byte[RefreshTokenByteLength];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return (token, expiresAtUtc);
    }
}
