using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// Scoped lifetime — one per request, shares the <see cref="IdentityDbContext"/> (§7).
///
/// All queries use parameterized predicates via LINQ — no raw SQL, no string concatenation (§9).
/// IQueryable never leaves this class; callers receive domain types only (§6).
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .AnyAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Set<User>().AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Set<User>().Update(user);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Loads the owning User plus all of their RefreshTokens so the aggregate
    /// is complete for domain operations (rotation, revocation).
    /// AsSplitQuery avoids a cartesian product between Users and RefreshTokens rows.
    /// </remarks>
    public async Task<User?> FindByRefreshTokenAsync(string tokenValue, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.TokenValue == tokenValue),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
