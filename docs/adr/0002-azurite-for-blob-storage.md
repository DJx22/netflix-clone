# ADR 0002: Azurite Instead of Real Azure Blob Storage

## Status
Accepted

## Context
The Streaming/Playback service needs blob storage for encoded video assets and thumbnails. An earlier draft of this plan assumed real Azure Blob Storage, based on prior hands-on Azure experience elsewhere.

This build currently has no confirmed Azure subscription or budget (roadmap §11, open items). A live cloud dependency also conflicts with Phase 2's checkpoint — `docker compose up` should bring up the whole platform from cold with zero manual steps, which a real cloud resource can't guarantee (account state, network availability, accidental cost from repeated test runs).

## Decision
Use Azurite — Microsoft's official local Azure Storage emulator — as its own container in `docker-compose.yml`, for Phases 0 through 8. The Streaming service's Infrastructure layer talks to Azurite through the same `Azure.Storage.Blobs` SDK it would use against real Azure Blob Storage. Only the connection string changes between the two.

## Consequences

**Positive**
- Zero cloud cost or account setup required to reach any Phase 0–8 checkpoint.
- `docker compose up` stays fully offline-capable, consistent with Phase 2.
- The SDK-level code written against Azurite is the same code that runs against real Azure Blob Storage — swapping the connection string is the only change needed if Phase 9 happens.

**Negative**
- Azurite doesn't perfectly replicate real Azure Storage — throttling behavior, latency, and some edge-case API responses differ. Anything specific to those is deferred to Phase 9, not covered by this build.

**Revisit trigger**
- Phase 9 (AKS cloud stretch), if it happens, swaps the connection string to a real Azure Storage account. Until then, this decision stands.
