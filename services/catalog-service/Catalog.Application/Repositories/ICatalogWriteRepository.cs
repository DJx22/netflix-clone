using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;

namespace Catalog.Application.Repositories;

/// <summary>
/// Write-side persistence contract for the <see cref="Title"/> aggregate.
/// Command handlers use this interface to load, persist, and remove aggregates.
/// </summary>
/// <remarks>
/// Technology-agnostic by design: no <c>IMongoCollection&lt;T&gt;</c>,
/// <c>BsonDocument</c>, or <c>FilterDefinition&lt;T&gt;</c> in any signature
/// (ADR 0005, §2, §16).  The MongoDB-specific implementation lives exclusively
/// in <c>Catalog.Infrastructure</c>.
///
/// Read-side projections are served by <see cref="ICatalogReadRepository"/> — this
/// interface covers only the four write operations the command side needs.
/// </remarks>
public interface ICatalogWriteRepository
{
    /// <summary>
    /// Loads the full <see cref="Title"/> aggregate for mutation by a command handler.
    /// Returns <c>null</c> when no title with the given <paramref name="titleId"/> exists.
    /// </summary>
    Task<Title?> FindByIdAsync(TitleId titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a newly created <see cref="Title"/> aggregate.
    /// </summary>
    Task AddAsync(Title title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored document for an existing <see cref="Title"/> with
    /// the aggregate's current state after a behavior method has been called.
    /// </summary>
    Task UpdateAsync(Title title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently removes the title identified by <paramref name="titleId"/>.
    /// </summary>
    Task DeleteAsync(TitleId titleId, CancellationToken cancellationToken = default);
}
