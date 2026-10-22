# Streaming Service — Known Deferrals & Phase Flags

Tracks deliberate, conscious deferrals from the current implementation that must be
addressed before specific future phases. This is not a bug list — every item here was a
correct decision for Phase 1–3 that will become incorrect in a later phase.

| § | Phase trigger | Risk if missed |
|---|---|---|
| [§TitleIdContractAlignment](#titleidcontractalignment) | Phase 4 / any manual seeding | Catalog diagnostic always returns `NotFound`; misleading logs |
| [§ConcurrentUpsertRace](#concurrentupsertrace) | ~~Phase 4~~ ✅ **Resolved in Phase 4** | Concurrent writes → HTTP 500 |
| [§EF-FindAsync-Syntax](#ef-findasync-syntax) | Any EF Core downgrade | Build break at runtime |
| [§NoMediaIngestionEndpoint](#nomediaingestionendpoint) | Any new ingestion endpoint | Dead code gap |
| [§CatalogBoundary](#catalogboundary) | Any fallback-source proposal | Architectural boundary violation |

---

## §TitleIdContractAlignment — Catalog and Streaming share no enforced cross-service identity

**Affects:** `MediaAsset.TitleId` (StreamingDb), `CatalogHttpClient.CheckTitleExistsAsync`,
`UpsertTitleRequest.StreamingAssetId` in Catalog

**Phase trigger:** Phase 4 — event-driven architecture / automated media ingestion.
Also actionable in any earlier phase where media assets are seeded programmatically.

**Detail:**

Catalog auto-generates its `TitleId` as a random GUID string at create time
(`TitleId.NewId() => new(Guid.NewGuid().ToString())`). That GUID is what Catalog
exposes as `titleId` in all its API responses (`GET /api/v1/titles/{titleId}`).

Streaming's `MediaAsset.TitleId` is a plain `string` column with no foreign-key or
cross-service validation. Nothing in either service enforces that the value stored in
`StreamingDb.MediaAssets.TitleId` equals any real Catalog `TitleId`.

Catalog does have a separate `streamingAssetId` field, but it only goes **Catalog → Streaming**
(a human-readable annotation on the Catalog record). It is never read by the Streaming
service and is not the same identifier.

**Consequence today (Phase 1–3):** The `CatalogHttpClient` diagnostic call after a
Streaming miss sends `GET /api/v1/titles/{titleId}` using whatever string is in
`StreamingDb.MediaAssets.TitleId`. This only returns Catalog's 200 (Found) if that
string is exactly the GUID Catalog assigned. If `MediaAssets` was seeded with a
human-readable slug like `"inception-2010"`, the call will always return 404 from
Catalog — making the diagnostic log misleading.

**Correct operational pattern for Phase 1–3:**
Always use the `titleId` from the Catalog `POST /api/v1/titles` response as the key
when seeding `StreamingDb.MediaAssets`:

```bash
# 1. Create the title in Catalog — it generates and returns the canonical titleId
TITLE_ID=$(curl -s -X POST http://localhost:5005/api/v1/titles \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Inception","genres":["Sci-Fi"],"releaseYear":2010,
       "maturityRating":"PG-13","durationMinutes":148}' \
  | jq -r '.titleId')

# 2. Seed Streaming with that same GUID string
INSERT INTO MediaAssets (TitleId, MediaUrl, ContentType, DurationSeconds)
VALUES ('<TITLE_ID>', 'http://...', 'video/mp4', 8880);
```

**Long-term fix (Phase 4):**
Catalog publishes a `TitleCreated { TitleId, StreamingAssetId }` domain event on the
message bus. The Streaming service subscribes and records the `TitleId` as the canonical
key when registering a new `MediaAsset`. This eliminates the manual convention entirely.
A formal ADR is required before that event contract is defined.

---

## §EF-FindAsync-Syntax — EF Core 10 array-literal `FindAsync` overload

**Affects:** `MediaAssetRepository.FindByTitleIdAsync`,
`PlaybackPositionRepository.FindAsync`

**Phase trigger:** Any EF Core version downgrade (8 or 9).

**Detail:**
Both repositories use the collection-literal overload of `FindAsync` introduced in
EF Core 10:

```csharp
// EF Core 10 only:
await _dbContext.MediaAssets.FindAsync([titleId], cancellationToken);
await _dbContext.PlaybackPositions.FindAsync([titleId, profileId], cancellationToken);
```

If the project is ever downgraded to EF Core 8 or 9, both call sites must be changed to:

```csharp
// EF Core 8/9 compatible:
await _dbContext.MediaAssets.FindAsync(new object[] { titleId }, cancellationToken);
await _dbContext.PlaybackPositions.FindAsync(new object[] { titleId, profileId }, cancellationToken);
```

**Action:** Update both repositories if `Streaming.Infrastructure.csproj` changes
`Microsoft.EntityFrameworkCore.SqlServer` to a version below `10.*`.

---

## §ConcurrentUpsertRace — Concurrent `PUT /position` double-insert produces HTTP 500

**Affects:** `PlaybackPositionRepository.UpsertAsync`, `PlaybackService.SavePositionAsync`

**✅ Resolved in Phase 4** (`PlaybackPositionRepository.UpsertAsync`).

The race was: two concurrent requests both see `null` from `FindAsync`, both attempt
`INSERT`, the second fails with SQL Server error 2627/2601, EF wraps it in
`DbUpdateException`, global handler maps to HTTP 500.

**Fix applied:** `UpsertAsync` now catches `DbUpdateException` where
`InnerException is SqlException { Number: 2627 or 2601 }`, detaches the conflicting
entry, re-fetches the row the winning thread inserted, applies
`UpdatePosition(...)`, and calls `SaveChangesAsync` again. The race is now transparent
to the caller — no HTTP 500.

---

## §NoMediaIngestionEndpoint — `IMediaAssetRepository` is read-only

**Affects:** `IMediaAssetRepository`, `MediaAssetRepository`

**Phase trigger:** Any phase that introduces a media-ingestion or media-management
endpoint to the Streaming service OpenAPI contract.

**Detail:**
The current `IMediaAssetRepository` exposes only `FindByTitleIdAsync`. No `AddAsync`,
`UpdateAsync`, or `DeleteAsync` methods exist because no corresponding endpoint is
defined in `openapi.yaml`. Adding write methods before the endpoint is defined would
be dead code and violates the "implement what the contract needs" principle.

**Action:** When a media-ingestion endpoint is added to `openapi.yaml`:
1. Add write methods to `IMediaAssetRepository`.
2. Implement them in `MediaAssetRepository`.
3. Add a corresponding Application service method and Application unit tests.

---

## §CatalogBoundary — Catalog result never changes `GetMedia` response

**Affects:** `PlaybackService.GetMediaAsync`, `CatalogHttpClient`

**Phase trigger:** Any proposal to use Catalog as a fallback media source.

**Detail:**
ADR 0006 §3 mandates that Streaming's `StreamingDb` is the sole source of truth for
whether the service can return playable media. The Catalog lookup in `GetMediaAsync`
is **diagnostic only** — it enriches logs but cannot change the 404 outcome.

This boundary is enforced by the type system (`GetMediaResult.NotFound` is always
returned after a miss, regardless of `CatalogTitleCheckResult`), and by an explicit
ownership-proof test:

```
GetMediaAsync_CatalogFoundTitle_NeverManufacturesMediaResponse
```

If a future requirement proposes using Catalog metadata to construct a `MediaResponse`,
that requires a formal ADR revision, not a code change in the current architecture.
