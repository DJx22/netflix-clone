using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="User"/> aggregate root.
/// One interface per aggregate root per §6 of the coding standard.
/// Implemented in Identity.Infrastructure; never referenced from Identity.Domain itself —
/// this interface lives here so Application can depend on it without a circular reference.
///
/// All methods accept a <see cref="CancellationToken"/> per §8.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns null when no user with the given ID exists.</summary>
    Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>Returns null when no user with the given email exists.</summary>
    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when an account already exists for this email address.
    /// Used by the registration use case to raise <see cref="Exceptions.EmailAlreadyRegisteredException"/>
    /// before attempting an insert.
    /// </summary>
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Persists a newly created user and its initial refresh tokens, if any.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists mutations to an existing user (refresh token rotation, revocation).
    /// Infrastructure implementations must update the aggregate and its owned collection.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user who owns the given refresh token value.
    /// Returns null when no matching, undeleted token exists.
    /// A dedicated query is necessary here — scanning all users' token collections
    /// in memory is not a safe option even at low traffic, and the caller cannot
    /// know the UserId from an opaque token string alone.
    /// </summary>
    Task<User?> FindByRefreshTokenAsync(string tokenValue, CancellationToken cancellationToken = default);
}
