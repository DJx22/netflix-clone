namespace Identity.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the "Jwt" configuration section.
/// Key material must come from environment variables or a secret store —
/// never hard-coded (coding standard §12).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing secret. Min 32 characters enforced at startup.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Expected issuer claim placed in every token (iss).</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Expected audience claim placed in every token (aud).</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Access token lifetime in minutes. Spec: 15 min (openapi.yaml line 245).</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 15;

    /// <summary>
    /// Refresh token lifetime in days.
    /// The spec does not state a value; a default of 7 days is conventional
    /// but this must be an explicit configuration decision before going to production.
    /// </summary>
    public int RefreshTokenExpiryDays { get; init; } = 7;
}
