using Catalog.Domain.Aggregates;
using Catalog.Domain.ValueObjects;
using MongoDB.Bson.Serialization;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Registers the <see cref="Title"/> aggregate and its value objects with the
/// MongoDB driver's <see cref="BsonClassMap"/> system.
/// </summary>
/// <remarks>
/// This is the only place in the codebase where <c>MongoDB.Driver</c> knows about
/// <see cref="Title"/> — keeping it here preserves Domain's zero-driver-reference
/// guarantee (ADR 0005, §2).
///
/// Calling convention: <see cref="Register"/> must be called once, before the first
/// <see cref="IMongoCollection{T}"/> is resolved.  DependencyInjection.cs calls it
/// during service registration, which happens before any request is processed.
///
/// <para>
/// <b>Value object serialization strategy:</b>
/// <see cref="Genre"/>, <see cref="MaturityRating"/>, and <see cref="TitleId"/>
/// are stored as plain BSON strings — they carry no document-level identity or
/// sub-fields that MongoDB needs to query on separately.  Custom serializers
/// convert each to/from its <c>Value</c> string so the stored document stays
/// flat and human-readable.  The alternative (storing them as subdocuments)
/// would complicate every filter and projection with nested field paths for no
/// indexing or querying benefit.
/// </para>
///
/// <para>
/// <b>Constructor binding:</b>
/// <see cref="BsonClassMap.MapCreator"/> is explicit about which constructor
/// overload MongoDB calls — parameter names do not need to match property names
/// the way EF Core's convention requires, but the expression must match the
/// actual constructor signature exactly.  If <see cref="Title"/>'s constructor
/// signature changes, this binding breaks at first deserialization, not at
/// compile time — the ADR's "Negative" consequence section calls this out explicitly.
/// </para>
/// </remarks>
public static class CatalogBsonConfiguration
{
    private static bool _isRegistered;

    // Guard flag is read/written only during startup registration (single-threaded
    // composition root).  No concurrent access possible; static mutable state is
    // acceptable here because it only gates a one-time idempotent operation (§16
    // forbids static mutable state *shared across requests*, not state written once
    // at startup before any request is handled).
    private static readonly object RegistrationLock = new();

    /// <summary>
    /// Registers all class maps required by the Catalog service.
    /// Safe to call multiple times — second and subsequent calls are no-ops.
    /// </summary>
    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (_isRegistered)
            {
                return;
            }

            RegisterTitleIdSerializer();
            RegisterGenreSerializer();
            RegisterMaturityRatingSerializer();
            RegisterTitleClassMap();

            _isRegistered = true;
        }
    }

    // -------------------------------------------------------------------------
    // Value object serializers — each maps a single-property value object to its
    // underlying string so the stored document stays flat.
    // -------------------------------------------------------------------------

    private static void RegisterTitleIdSerializer()
    {
        BsonSerializer.RegisterSerializer(new TitleIdSerializer());
    }

    private static void RegisterGenreSerializer()
    {
        BsonSerializer.RegisterSerializer(new GenreSerializer());
    }

    private static void RegisterMaturityRatingSerializer()
    {
        BsonSerializer.RegisterSerializer(new MaturityRatingSerializer());
    }

    // -------------------------------------------------------------------------
    // Title aggregate class map
    // -------------------------------------------------------------------------

    private static void RegisterTitleClassMap()
    {
        BsonClassMap.RegisterClassMap<Title>(cm =>
        {
            cm.AutoMap();

            // TitleId.Value becomes the BSON _id field.  The TitleIdSerializer
            // handles the string-to-TitleId round-trip.
            cm.MapIdMember(t => t.TitleId);

            // Explicit MapCreator because Title has no parameterless constructor —
            // this is the only way MongoDB knows how to reconstitute the aggregate.
            // The six required parameters match the constructor's required parameter list.
            // Optional properties (Description, Cast, PosterUrl, StreamingAssetId) are
            // set by AutoMap via the private setters that the driver accesses via reflection.
            cm.MapCreator(t => new Title(
                t.TitleId,
                t.Name,
                t.Genres,
                t.ReleaseYear,
                t.MaturityRating,
                t.DurationMinutes));

            // AutoMap picks up the remaining properties through their private setters.
            // No explicit mapping needed for Description, Cast, PosterUrl, StreamingAssetId
            // as long as their property names match the stored BSON field names.
        });
    }
}
