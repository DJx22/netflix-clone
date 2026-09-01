using Microsoft.EntityFrameworkCore;
using Profile.Domain.Entities;
using Profile.Domain.Repositories;
using Profile.Domain.ValueObjects;

namespace Profile.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProfileRepository"/>.
/// Scoped lifetime — one per request, shares the <see cref="ProfileDbContext"/> (§7).
///
/// All queries use parameterized predicates via LINQ — no raw SQL, no string concatenation (§9).
/// IQueryable never leaves this class; callers receive domain types only (§6).
/// </summary>
internal sealed class ProfileRepository : IProfileRepository
{
    private readonly ProfileDbContext _context;

    public ProfileRepository(ProfileDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> FindByIdAsync(ProfileId id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserProfile>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserProfile>> FindByAccountIdAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserProfile>()
            .Where(p => p.AccountId == accountId)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> CountByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserProfile>()
            .CountAsync(p => p.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.Set<UserProfile>().AddAsync(profile, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _context.Set<UserProfile>().Update(profile);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _context.Set<UserProfile>().Remove(profile);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
