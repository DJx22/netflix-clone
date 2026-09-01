namespace Profile.Api.Options;

/// <summary>
/// Strongly-typed binding for JWT validation settings.
/// Profile validates incoming JWTs but does not issue them — that's Identity's job.
/// These values must match the issuer/audience/key that Identity uses to sign tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric key used by Identity to sign tokens. Must match exactly.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Expected iss claim value.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected aud claim value.</summary>
    public string Audience { get; init; } = string.Empty;
}
