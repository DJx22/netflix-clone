using Catalog.Application.DTOs;
using Catalog.Application.Repositories;
using Catalog.Domain.Aggregates;
using MongoDB.Driver;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="ICatalogReadRepository"/>.
/// </summary>
/// <remarks>
/// MongoDB filters and pagination run on the server, then the registered serializers
/// deserialize the matching documents before this class maps them to DTOs.  This
/// avoids asking the driver's LINQ translator to translate value-object members.
///
/// All filter expressions use the driver's typed <see cref="Builders{T}"/> API.
/// No raw BSON string filters, no <c>$where</c>, no string-built JSON — consistent
/// with §9's "parameterized queries only" rule (§16).
///
/// The search filter uses MongoDB's <c>$text</c> operator, which requires the
/// <c>ix_titles_name_text</c> text index defined in <see cref="Indexes.TitleIndexes"/>.
/// If that index is absent, <c>SearchAsync</c> throws a <c>MongoCommandException</c>
/// at runtime — a deliberate fail-fast, not a silent fallback to a collection scan.
///
/// Lifetime: Scoped, matching <see cref="CatalogWriteRepository"/> and every other
/// repository in this codebase (ADR 0005).
/// </remarks>
public sealed class CatalogReadRepository : ICatalogReadRepository
{
    private readonly IMongoCollection<Title> _titles;

    public CatalogReadRepository(IMongoCollection<Title> titles)
    {
        _titles = titles;
    }

    /// <inheritdoc />
    public async Task<TitleDetailDto?> FindDetailByIdAsync(
        string titleId,
        CancellationToken cancellationToken = default)
    {
        // Filter on _id — the TitleIdSerializer writes TitleId.Value as the BSON string
        // stored in the _id field.  Filtering on "_id" directly avoids any ambiguity
        // about how the driver resolves the nested .Value property through the serializer.
        var filter = Builders<Title>.Filter.Eq("_id", titleId);

        var title = await _titles
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return title is null ? null : ToDetailDto(title);
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<TitleSummaryDto>> SearchAsync(
        string? search,
        string? genre,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildSearchFilter(search, genre);

        // Total count and the paged slice run as two separate queries.
        // MongoDB's aggregation pipeline can combine them into one round-trip
        // via $facet, but that adds complexity without a proven latency benefit
        // at this project's scale.  Revisit if profiling shows this as a hot path.
        var totalCount = await _titles
            .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var skip = (page - 1) * pageSize;

        var titles = await _titles
            .Find(filter)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = titles.Select(ToSummaryDto).ToList();

        return new PagedResultDto<TitleSummaryDto>(
            items,
            page,
            pageSize,
            (int)totalCount);
    }

    private static TitleSummaryDto ToSummaryDto(Title title) => new(
        title.TitleId.Value,
        title.Name,
        title.Genres.Select(g => g.Value).ToList(),
        title.ReleaseYear,
        title.PosterUrl);

    private static TitleDetailDto ToDetailDto(Title title) => new(
        title.TitleId.Value,
        title.Name,
        title.Genres.Select(g => g.Value).ToList(),
        title.ReleaseYear,
        title.PosterUrl,
        title.Description,
        title.Cast.ToList(),
        title.DurationMinutes,
        title.MaturityRating.Value,
        title.StreamingAssetId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListDistinctGenresAsync(
        CancellationToken cancellationToken = default)
    {
        // Genres serialize to a BSON string array via GenreSerializer.
        // Distinct<string> on the field name "Genres" asks MongoDB to return one
        // entry per unique value in the array across all documents — the driver
        // unwinds the array automatically for Distinct queries.
        // Using a string field name here rather than a lambda because the lambda
        // would resolve to the Genre value-object type, not the underlying BSON
        // string, causing a type mismatch with the stored document shape.
        var genres = await _titles
            .Distinct<string>(
                "Genres",
                Builders<Title>.Filter.Empty,
                cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return genres.Order().ToList().AsReadOnly();
    }

    // -------------------------------------------------------------------------
    // Filter builder — kept private; no FilterDefinition<T> crosses the class
    // boundary (ADR 0005, §16)
    // -------------------------------------------------------------------------

    private static FilterDefinition<Title> BuildSearchFilter(string? search, string? genre)
    {
        // Start with an empty filter (matches everything) and AND in each
        // optional predicate.  The typed Builders<T>.Filter API is the Mongo
        // equivalent of parameterized SQL — no string building, no injection risk.
        var filter = Builders<Title>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            // $text operator — requires ix_titles_name_text to exist.
            // Fails fast with a MongoCommandException if the index is absent,
            // which is preferable to silently falling back to a collection scan.
            filter &= Builders<Title>.Filter.Text(search, new TextSearchOptions
            {
                CaseSensitive = false,
                DiacriticSensitive = false
            });
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            // AnyEq on the "Genres" field (string array in BSON) — matches documents
            // where the genres array contains the given genre label.  String field name
            // used rather than a lambda because the lambda resolves to the Genre value-
            // object type, not the BSON string that GenreSerializer stores.
            // This hits the ix_titles_genres index defined in TitleIndexes.
            filter &= Builders<Title>.Filter.AnyEq("Genres", genre);
        }

        return filter;
    }
}
