using Catalog.Domain.Aggregates;
using MongoDB.Driver;

namespace Catalog.Infrastructure.Indexes;

/// <summary>
/// Defines and applies all indexes for the <c>titles</c> collection.
/// </summary>
/// <remarks>
/// This class is the MongoDB equivalent of EF Core migration files: index
/// definitions live in source control, are applied programmatically, and are
/// never created by hand in a shared environment (§9, adapted for a document store
/// per ADR 0005 — ADR 0005's "mapping is wrong" flag: index changes follow the
/// same pipeline discipline as relational migrations, even though MongoDB's
/// <c>CreateIndex</c> is idempotent by default, which relational migrations are not.
/// The discipline matters more than the technical idempotency).
///
/// <c>CreateManyAsync</c> is safe to call on every startup: MongoDB's
/// <c>CreateIndex</c> operation is a no-op when an identical index already exists.
/// It only fails (or recreates) if the same name is reused with a different
/// definition — so changing an index definition here requires a corresponding
/// manual drop of the old index in any environment that has already applied the
/// previous version.  That constraint is intentional: it makes schema changes
/// visible, not silent.
///
/// <para><b>Indexes defined:</b></para>
/// <list type="bullet">
///   <item>
///     <c>ix_titles_genres</c> — ascending on <c>genres</c>.  Supports the
///     <c>GET /titles?genre=X</c> filter path without a collection scan.
///   </item>
///   <item>
///     <c>ix_titles_name_text</c> — text index on <c>name</c>.  Supports the
///     <c>GET /titles?search=X</c> free-text search path.  MongoDB allows only
///     one text index per collection; if additional fields need full-text search
///     later, add them to this same index definition, not as a second text index.
///   </item>
/// </list>
///
/// The <c>_id</c> field (mapped to <c>TitleId.Value</c> via
/// <c>CatalogBsonConfiguration</c>) has MongoDB's default unique index — no
/// explicit definition needed here.
/// </remarks>
public static class TitleIndexes
{
    /// <summary>
    /// Applies all <c>titles</c> collection indexes idempotently.
    /// Call once at startup, before the service begins handling requests.
    /// </summary>
    public static async Task ApplyAsync(
        IMongoCollection<Title> collection,
        CancellationToken cancellationToken = default)
    {
        var indexModels = new List<CreateIndexModel<Title>>
        {
            BuildGenreIndex(),
            BuildNameTextIndex()
        };

        await collection
            .Indexes
            .CreateManyAsync(indexModels, cancellationToken)
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Index builders — each returns one CreateIndexModel.
    // Using Builders<Title>.IndexKeys (the driver's typed fluent API) rather than
    // BsonDocument-literal index specs, consistent with the "typed builders only,
    // never raw BSON" rule that mirrors §9's "parameterized queries only" (§16).
    // -------------------------------------------------------------------------

    private static CreateIndexModel<Title> BuildGenreIndex()
    {
        var keys = Builders<Title>.IndexKeys.Ascending(t => t.Genres);

        return new CreateIndexModel<Title>(
            keys,
            new CreateIndexOptions
            {
                Name = "ix_titles_genres",
                // Not unique — multiple titles share the same genre label.
                Background = false // foreground index build; acceptable at startup before traffic starts
            });
    }

    private static CreateIndexModel<Title> BuildNameTextIndex()
    {
        // Text index on Name only — description text is long and would significantly
        // inflate the text index size.  Revisit if full-description search is needed.
        var keys = Builders<Title>.IndexKeys.Text(t => t.Name);

        return new CreateIndexModel<Title>(
            keys,
            new CreateIndexOptions
            {
                Name = "ix_titles_name_text"
                // MongoDB enforces one text index per collection — any future full-text
                // fields must be added here, not as a second text index definition.
            });
    }
}
