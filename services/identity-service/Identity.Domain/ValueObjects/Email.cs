using System.Text.RegularExpressions;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// Validated, case-normalised email address value object.
/// Format validation is done here, not in Application, because "an email must look
/// like an email" is a domain rule (openapi.yaml: email field, format: email).
/// Uniqueness is a persistence concern and is enforced by the repository / database.
/// </summary>
public sealed class Email
{
    // RFC-5321 allows up to 254 characters in a full address.
    public const int MaxLength = 254;

    // Deliberately minimal: local@domain.tld. Full RFC 5322 is impractical to enforce
    // and every real email address matches this pattern.
    private static readonly Regex FormatRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a validated <see cref="Email"/> from a raw string.
    /// Normalises to lower-case so that storage and comparison are unambiguous.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the address fails format validation.</exception>
    public static Email From(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Email address cannot be empty.", nameof(raw));
        }

        var normalised = raw.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            throw new ArgumentException($"Email address exceeds the maximum length of {MaxLength} characters.", nameof(raw));
        }

        if (!FormatRegex.IsMatch(normalised))
        {
            throw new ArgumentException($"'{raw}' is not a valid email address.", nameof(raw));
        }

        return new Email(normalised);
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj) =>
        obj is Email other && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
}
