# ADR 0005: MongoDB for Catalog — Justification, Repository Pattern, and Mapping

## Status
Accepted

## Context
Two gaps, not one.

**First:** `csharp-coding-standard.md` §9 specifies "writes go through EF Core; high-traffic reads go through Dapper" — both SQL-specific. It gives Catalog's Infrastructure layer nothing to follow for a document store. Left unresolved, this gets decided ad hoc, inconsistently with how every other service was built.

**Second:** ADR 0001 justified consolidating SQL Server against the 8GB RAM budget. MongoDB never got the same scrutiny — it's a full separate container and a full separate technology (different driver, no EF Core, no relational integrity), paid for on the same constrained machine, and that cost was never actually weighed against the alternative of just using SQL Server for Catalog too.

## Decision

**Why MongoDB survives the RAM cost.** Title/genre/metadata shape varies by content type (episodic vs. film, variable cast/genre structures) in a way a document model fits more naturally than a rigid relational schema — and running one non-relational datastore deliberately is itself part of the point of this project (polyglot persistence is a real microservices lesson, not just Catalog's problem to solve alone). Given the choice between paying the RAM cost or folding Catalog into the shared SQL Server container and losing that lesson entirely, the RAM cost is worth it — but only because that lesson is real, not by default.

**Repository pattern.** `ICatalogRepository` still lives in Application, implemented in Infrastructure — same DIP boundary as every SQL-backed service (§5). The implementation uses `IMongoCollection<Title>`, not EF Core, not Dapper. Same rule as "never leak `IQueryable`" (§6), generalized: never leak `IMongoCollection`, `BsonDocument`, or any `FilterDefinition<T>` out of Infrastructure.

**Single model, not a parallel document type.** Consistent with ADR 0004: `Title` is both the domain aggregate and what MongoDB serializes, mapped via `BsonClassMap` registered in Infrastructure — not `[Bson...]` attributes on the Domain class, which would put a MongoDB.Driver reference directly in Domain and break §2's "no framework references" rule outright.

```csharp
// Domain/Title.cs — zero MongoDB.Driver reference
public sealed class Title
{
    public string TitleId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<string> Genres { get; private set; }
    public int ReleaseYear { get; private set; }
    public string MaturityRating { get; private set; }
    public IReadOnlyList<string> Cast { get; private set; } = Array.Empty<string>();
    public int DurationMinutes { get; private set; }
    public string? PosterUrl { get; private set; }
    public string? StreamingAssetId { get; private set; }

    public Title(string titleId, string name, IReadOnlyList<string> genres, int releaseYear,
                 string maturityRating, int durationMinutes)
    {
        // validate, assign required fields
    }

    public void UpdateMetadata(string name, string? description, IReadOnlyList<string> genres,
                                string maturityRating, IReadOnlyList<string> cast,
                                int durationMinutes, string? posterUrl, string? streamingAssetId)
    {
        // reassign, whatever invariants apply
    }
}
```

```csharp
// Infrastructure/CatalogBsonConfiguration.cs — the only place that knows Title exists to MongoDB
public static class CatalogBsonConfiguration
{
    public static void Register()
    {
        BsonClassMap.RegisterClassMap<Title>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(t => t.TitleId);
            cm.MapCreator(t => new Title(t.TitleId, t.Name, t.Genres, t.ReleaseYear, t.MaturityRating, t.DurationMinutes));
        });
    }
}
```
Unlike EF Core, this binding is **explicit**, not convention-matched by parameter name — `MapCreator` has to be told exactly how to call the constructor. Check the exact method names above against your installed MongoDB.Driver version; this API surface has shifted across versions and isn't worth asserting precisely from memory.

**Reads bypass the aggregate entirely — this is what CQRS is actually for here.** Catalog is the one service using Mediator/CQRS (§6, roadmap Phase 1). Query handlers project straight to the OpenAPI spec's `TitleSummary` shape via MongoDB's native projection — they never materialize a full `Title`:

```csharp
var summaries = await _titles
    .Find(filter)
    .Project(t => new TitleSummaryDto(t.TitleId, t.Name, t.Genres, t.ReleaseYear, t.PosterUrl))
    .ToListAsync(cancellationToken);
```
Command handlers (create/update/delete) load the full `Title` and call its behavior methods. This is the same read/write split already established for Subscription in spirit — command side protects invariants through the aggregate, query side skips it — except here it was already architecturally justified by Catalog's CQRS status, not something to debate per-aggregate the way ADR 0004 had to for Subscription.

**Lifetimes.** `IMongoClient` and `IMongoDatabase` → Singleton (§7: "stateless services/clients → Singleton"; this also matches MongoDB's own driver guidance — `MongoClient` manages its own connection pooling and is explicitly meant to be shared). The repository implementation itself stays **Scoped**, matching §7's blanket "repositories → Scoped" rule, even though a Mongo-backed repository has no technical requirement to be — it holds no per-request state. Flagging this explicitly rather than silently deviating, per §1: Singleton would be a valid micro-optimization, not worth having one repository out of six follow a different lifetime rule than the rest for an unmeasured gain.

**Connection string** bound via `IOptions<MongoDbOptions>`, registered in `DependencyInjection.cs` — not read from `IConfiguration` directly in the client registration lambda, same rule as every other service (§6).

## Consequences

**Positive**
- Catalog's repository now has a written convention to follow, instead of getting decided ad hoc mid-implementation.
- The MongoDB cost is justified against the same constraint (RAM) that governs every other technology choice in this project, not exempted from scrutiny by being the "obvious" polyglot pick.
- Domain stays framework-free — same guarantee EF-backed services get, via the same mechanism (external mapping configuration, not attributes).
- CQRS's read side does real, measurable work here (skips full-aggregate materialization for every search/list call) instead of being ceremony.

**Negative**
- `BsonClassMap`'s explicit `MapCreator` is one more thing to keep in sync by hand if `Title`'s constructor signature changes — EF's convention-based binding doesn't need this, so the two data-access layers in this codebase now have subtly different failure modes for the same class of mistake.
- MongoDB adds a technology surface (aggregation pipeline, index management, no cross-collection ACID transactions) with no equivalent elsewhere in the project to reuse learning from.

**Revisit trigger**
- If 8GB genuinely can't sustain SQL Server + MongoDB + Redis + Azurite + RabbitMQ (even one at a time, deliberately, per roadmap §0), MongoDB is the one polyglot choice that could fold into a SQL Server table with a JSON column as a last resort — at the direct cost of the polyglot-persistence lesson this ADR just argued for keeping. Not a default; a fallback if the RAM budget actually fails in practice, not in theory.
