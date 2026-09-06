using Catalog.Application.Repositories;
using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using MongoDB.Driver;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="ICatalogWriteRepository"/>.
/// </summary>
/// <remarks>
/// This is the only class in the write path that holds an
/// <see cref="IMongoCollection{T}"/> reference.  No driver type crosses the
/// class boundary — all public members use only Domain and Application types
/// (ADR 0005, §2, §16).
///
/// All filter and update expressions use the driver's typed
/// <see cref="Builders{T}"/> API.  No raw BSON strings, no
/// <c>BsonDocument</c>-literal filters — consistent with §9's
/// "parameterized queries only" rule applied to a document store.
///
/// Lifetime: Scoped.  The repository holds no per-request state of its own —
/// the Scoped lifetime is kept to match every other repository in this codebase
/// rather than optimizing for Singleton, per ADR 0005's explicit note on this
/// trade-off.
/// </remarks>
public sealed class CatalogWriteRepository : ICatalogWriteRepository
{
    private readonly IMongoCollection<Title> _titles;

    public CatalogWriteRepository(IMongoCollection<Title> titles)
    {
        _titles = titles;
    }

    /// <inheritdoc />
    public async Task<Title?> FindByIdAsync(
        TitleId titleId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Title>.Filter.Eq(t => t.TitleId, titleId);

        return await _titles
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        Title title,
        CancellationToken cancellationToken = default)
    {
        await _titles
            .InsertOneAsync(title, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Title title,
        CancellationToken cancellationToken = default)
    {
        // ReplaceOne with the full document is the correct choice here:
        // the command handler has already called a behavior method on the aggregate,
        // so every field that changed is already in the in-memory object.  A partial
        // $set update would require the handler to know which fields changed, which
        // violates SRP (§5) — the handler knows *what* changed; Infrastructure decides
        // *how* to persist it.
        var filter = Builders<Title>.Filter.Eq(t => t.TitleId, title.TitleId);

        await _titles
            .ReplaceOneAsync(filter, title, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        TitleId titleId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Title>.Filter.Eq(t => t.TitleId, titleId);

        await _titles
            .DeleteOneAsync(filter, cancellationToken)
            .ConfigureAwait(false);
    }
}
