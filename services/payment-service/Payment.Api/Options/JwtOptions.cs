namespace Payment.Api.Options;

/// <summary>
/// Strongly-typed binding for JWT validation settings.
/// Payment validates incoming JWTs but does not issue them — that is Identity's job.
/// These values must match the issuer, audience, and signing key that Identity uses.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Symmetric key used by Identity to sign tokens. Must match exactly. Never committed to source — injected at runtime.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Expected <c>iss</c> claim value.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected <c>aud</c> claim value.</summary>
    public string Audience { get; init; } = string.Empty;
}
