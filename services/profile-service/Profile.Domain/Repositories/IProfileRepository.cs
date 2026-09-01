using Profile.Domain.Entities;
using Profile.Domain.ValueObjects;

namespace Profile.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="UserProfile"/> aggregate root.
/// One interface per aggregate root per §6 of the coding standard.
/// Implemented in Profile.Infrastructure; never referenced from Profile.Domain itself —
/// this interface lives here so Application can depend on it without a circular reference.
///
/// All methods accept a <see cref="CancellationToken"/> per §8.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Returns null when no profile with the given ID exists.</summary>
    Task<UserProfile?> FindByIdAsync(ProfileId id, CancellationToken cancellationToken = default);

    /// <summary>Returns all profiles belonging to the given account.</summary>
    Task<IReadOnlyList<UserProfile>> FindByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current number of profiles for an account.
    /// Used by Application-layer validation to enforce the per-account profile limit (5)
    /// before calling <see cref="UserProfile.Create"/>.
    /// </summary>
    Task<int> CountByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>Persists a newly created profile.</summary>
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists mutations to an existing profile (display name change, preference updates).
    /// Infrastructure implementations must detect and flush the changes.
    /// </summary>
    Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Removes a profile by its ID.</summary>
    Task DeleteAsync(UserProfile profile, CancellationToken cancellationToken = default);
}
