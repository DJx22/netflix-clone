using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;

namespace Identity.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of <see cref="IPasswordHasher"/>.
/// Work factor 12 is the cost setting — high enough to be meaningful against
/// offline attacks, low enough that a login under normal load is not perceptibly slow.
/// This is the only place in the codebase where raw passwords are processed.
///
/// Registered as Singleton: BCrypt.Net is stateless and thread-safe. All state
/// that varies per call is passed in as parameters (§7).
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc/>
    public PasswordHash Hash(Password password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password.Value, WorkFactor);
        return PasswordHash.From(hash);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// BCrypt.Net performs a constant-time comparison internally —
    /// the implementation here must not short-circuit before the library call.
    /// </remarks>
    public bool Verify(Password password, PasswordHash hash)
    {
        return BCrypt.Net.BCrypt.Verify(password.Value, hash.Value);
    }
}
