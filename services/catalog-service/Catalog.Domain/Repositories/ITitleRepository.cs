using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;

namespace Catalog.Domain.Repositories;

/// <summary>
/// Write-side persistence contract for the <see cref="Title"/> aggregate.
/// </summary>
/// <remarks>
/// Per ADR 0005 and §5 (DIP): the interface lives in Domain; the implementation
/// lives in Infrastructure and uses <c>IMongoCollection&lt;Title&gt;</c>.  Nothing
/// in Domain or Application references that driver type.
///
/// This interface covers only the commands the write side needs (create, replace,
/// delete, and load-for-update).  Query projections for the read side are served
/// directly from MongoDB inside query handlers and are not expressed here.
/// </remarks>
public interface ITitleRepository
{
    /// <summary>
    /// Retrieves the full <see cref="Title"/> aggregate for a given identifier,
    /// or <c>null</c> if no such title exists.
    /// </summary>
    /// <remarks>
    /// Command handlers call this before mutating state.  Query handlers bypass
    /// the aggregate entirely (ADR 0005 — reads project straight from MongoDB).
    /// </remarks>
    /// <param name="titleId">The identifier of the title to load.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task<Title?> FindByIdAsync(TitleId titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly created <see cref="Title"/> aggregate.
    /// </summary>
    /// <param name="title">The aggregate to insert; must not already exist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task AddAsync(Title title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored document for an existing <see cref="Title"/> with
    /// the current aggregate state.
    /// </summary>
    /// <param name="title">The mutated aggregate to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task UpdateAsync(Title title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the <see cref="Title"/> with the specified identifier.
    /// </summary>
    /// <param name="titleId">Identifier of the title to delete.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task DeleteAsync(TitleId titleId, CancellationToken cancellationToken = default);
}
