namespace Identity.Domain.ValueObjects;

/// <summary>
/// Wraps an opaque password hash string produced by Infrastructure.
/// The domain never sees or handles raw passwords after the User is created —
/// the hash is the only representation that crosses the persistence boundary.
/// </summary>
public sealed class PasswordHash
{
    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    /// <exception cref="ArgumentException">Thrown when the hash string is null or empty.</exception>
    public static PasswordHash From(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(hash));
        }

        return new PasswordHash(hash);
    }

    public override string ToString() => Value;
}
