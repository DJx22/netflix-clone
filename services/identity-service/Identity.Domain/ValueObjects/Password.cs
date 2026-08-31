namespace Identity.Domain.ValueObjects;

/// <summary>
/// Represents a raw, unvalidated password as received from the caller before hashing.
/// This type exists only to carry the plaintext safely through the domain boundary into
/// Infrastructure where hashing occurs. It is never persisted.
///
/// Domain rules enforced here (openapi.yaml line 215-216):
///   - Minimum 10 characters.
///   - At least one digit (0-9).
///
/// Confirming that password == confirmPassword is a request-level concern (Application layer).
/// </summary>
public sealed class Password
{
    public const int MinLength = 10;

    public string Value { get; }

    private Password(string value)
    {
        Value = value;
    }

    /// <exception cref="ArgumentException">Thrown when the password violates domain rules.</exception>
    public static Password From(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(raw));
        }

        if (raw.Length < MinLength)
        {
            throw new ArgumentException(
                $"Password must be at least {MinLength} characters long.", nameof(raw));
        }

        // At least one digit is explicitly required by the spec (openapi.yaml line 216).
        if (!ContainsDigit(raw))
        {
            throw new ArgumentException("Password must contain at least one number (0-9).", nameof(raw));
        }

        return new Password(raw);
    }

    private static bool ContainsDigit(string value)
    {
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                return true;
            }
        }
        return false;
    }
}
