using Identity.Domain.ValueObjects;

namespace Identity.Domain.Repositories;

/// <summary>
/// Abstraction over the password hashing algorithm.
/// The algorithm choice (bcrypt, Argon2, etc.) and its cost factor are
/// Infrastructure decisions that must not leak into the domain.
/// Placed under Repositories/ to keep all outward-facing ports together, though
/// strictly this is a "service port" rather than a repository.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a raw password and returns the opaque hash for storage.</summary>
    PasswordHash Hash(Password password);

    /// <summary>
    /// Returns true when the raw password matches the stored hash.
    /// Constant-time comparison is the responsibility of the implementation.
    /// </summary>
    bool Verify(Password password, PasswordHash hash);
}
