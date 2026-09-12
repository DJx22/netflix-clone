# ADR 0006: Streaming Service Data Store and Media Lookup

**Status:** Accepted  
**Date:** 2026-09-12  
**Deciders:** Dhruv (project owner)

## Context

The original Phase 0 roadmap named Redis as a "playback-position cache" for Streaming, but no ADR was finalized before implementation began. When the repository design was reached, two questions remained unresolved: the persistence model for playback positions and the boundary between Streaming and Catalog when looking up a `titleId`.

Streaming needs to persist two things:

1. Media location/technical metadata per title — `mediaUrl`, `contentType`, `durationSeconds`.
2. Per-profile playback position per title — `positionSeconds`, `updatedAtUtc`.

The project's learning goals are microservices architecture, Docker, Podman, Kubernetes, and Jenkins. Streaming is not intended to become a persistence-technology exercise. The other core relational services already use SQL Server on a shared SQL Server 2022 container, with one logical database per service.

## Decisions

### 1. SQL Server is the Streaming persistence store

Streaming will use its own logical SQL Server database, `StreamingDb`, on the shared SQL Server 2022 container.

It stores:

- **Media metadata:** `titleId`, `mediaUrl`, `contentType`, `durationSeconds`
- **Playback position:** `titleId`, `profileId`, `positionSeconds`, `updatedAtUtc`

Actual media files remain in Azurite. `StreamingDb` stores the media URL and technical metadata, never the binary content.

Persistence is plain CRUD through EF Core. Streaming does not use Mediator/CQRS, Redis, Dapper, or another persistence technology.

### 2. Streaming owns the media metadata required to serve playback

Catalog owns descriptive title metadata such as the title name, genre, and search information. Streaming owns the playback-specific media metadata required by its API: `mediaUrl`, `contentType`, and `durationSeconds`.

The `titleId` is therefore used as the cross-service identifier, but Streaming remains responsible for determining whether it has a playable `MediaAsset`.

### 3. Media lookup checks Streaming first, then Catalog for diagnostic validation

For `GET /api/v1/playback/{titleId}/media`:

1. Streaming first looks up the `titleId` in its own `StreamingDb`.
2. If a `MediaAsset` exists, Streaming returns the media response. No Catalog call is required.
3. If the `MediaAsset` does not exist:
   - Streaming logs that the media asset was not found in its own database.
   - Streaming then calls Catalog to check whether the same `titleId` exists there.
   - If Catalog contains the `titleId`, Streaming logs that the title exists in Catalog.
   - If Catalog does not contain the `titleId`, Streaming logs that the title was not found in Catalog.
   - Regardless of the Catalog result, Streaming returns the API's `404 Not Found` because Streaming itself has no `MediaAsset` for that `titleId`.

The Catalog lookup is therefore **diagnostic/cross-service validation**, not a fallback source of media data. Streaming never returns Catalog metadata from this endpoint and never treats a Catalog hit as proof that playable media exists.

This keeps the bounded-context ownership clear: Catalog can confirm that a title exists, but only Streaming can supply the media asset required by this endpoint.

The Catalog call must not be performed when the Streaming database already contains the media asset.

### 4. Playback-position retention is explicitly excluded

No TTL, expiry timestamp, automatic deletion, scheduled cleanup, or retention policy is implemented for `PlaybackPosition`.

`UpdatedAtUtc` remains part of the stored playback position because it is part of the data model, but it is **not** used as an expiry mechanism.

If retention becomes a real product requirement later, it will be addressed as a separate decision and implementation.

## Consequences

### Positive

- Streaming has one relational persistence technology consistent with the other SQL-backed core services.
- The project avoids adding Redis solely to store playback positions.
- Media ownership is explicit: Catalog owns descriptive metadata; Streaming owns playback/media metadata.
- The Streaming API does not blindly assume that every Catalog title has playable media.
- A missing Streaming asset can still be diagnosed against Catalog without changing the API contract.
- The 404 behavior remains deterministic: no Streaming `MediaAsset` means no media response.
- No retention machinery is introduced without an actual requirement.

### Negative / trade-offs

- A missing media asset causes an additional synchronous Catalog call.
- Streaming now has an HTTP dependency on Catalog for the missing-asset diagnostic path. That dependency must be handled as a dependency failure rather than allowing it to turn an expected media 404 into an unhandled exception.
- A title may exist in Catalog while having no Streaming `MediaAsset`; that state is valid and results in 404 from Streaming.
- The core project does not get hands-on Redis persistence experience. If Redis is used later, it should solve a separately stated problem.

## Alternatives considered

### Redis as the primary playback-position store

Rejected. It introduces persistence-mode and operational decisions that are not required by the project's current learning goals.

### Redis as a cache in front of SQL Server

Rejected for now. The project is Postman/Swagger-driven and does not have real playback traffic that justifies cache invalidation and another runtime dependency.

### Trust `titleId` without contacting Catalog

Rejected as the final behavior. Streaming still treats its own `StreamingDb` as the source of truth for playable media, but after a local miss it checks Catalog so the system can distinguish and log:

- title exists in Catalog but has no media asset in Streaming; or
- title does not exist in either service.

The Catalog result does not change the Streaming 404 response.

### Use Catalog as the source of media metadata

Rejected. Catalog owns descriptive title metadata; Streaming owns playback-specific media metadata and media location. Streaming must not use Catalog as a substitute for its own `MediaAsset` record.

## Open questions

There are **no remaining open questions from the two Streaming design items addressed by this ADR**.

- **Playback retention/TTL:** explicitly excluded for this build.
- **Catalog validation:** decided as a local-first lookup followed by a Catalog diagnostic lookup only after a local miss.

Future decisions may revisit these choices if requirements change.
