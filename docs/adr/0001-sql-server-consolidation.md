# ADR 0001: Single SQL Server Container, One Database per Service

## Status
Accepted

## Context
Four core services — Identity, Profile, Subscription, Payment — use SQL Server for relational/transactional data. `csharp-coding-standard.md` §9 requires one database per service, and by default that would mean four separate SQL Server containers.

Each SQL Server instance needs roughly 1.4GB+ RAM at minimum. Four of them, alongside MongoDB, Redis, RabbitMQ, and later a local Kubernetes cluster and Jenkins, do not fit inside this project's 8GB RAM budget (roadmap §0, §3).

## Decision
Run a single SQL Server 2022 container. Inside it, create four separate databases — `IdentityDb`, `ProfileDb`, `SubscriptionDb`, `PaymentDb` — one per service, each with its own connection string. No service's Infrastructure layer is permitted to hold a connection string pointing at another service's database.

The repository pattern boundary (one repository interface per aggregate root, implemented in that service's own Infrastructure project) is the enforcement point for this rule. Nothing about the shared container enforces it automatically.

## Consequences

**Positive**
- Cuts the local RAM footprint of relational data roughly 4x versus one container per service.
- Still fully exercises EF Core migrations, connection strings, and the repository pattern independently per service — the databases are logically separate even though the container isn't.

**Negative**
- Doesn't teach the operational reality of patching, scaling, or failing over four independent SQL Server instances. That's a real gap against a production setup, traded deliberately for RAM headroom.
- **Isolation here is discipline-enforced, not infrastructure-enforced.** A typo'd connection string is one config change away from crossing a service boundary that a separate container would have made structurally impossible. Treat this as a real limitation of the local setup, not a detail to gloss over — if this project ever handled real data, this trade would need to be revisited.

**Revisit trigger**
- If Phase 9 (AKS) happens, use Azure SQL there — separate logical databases or separate Azure SQL instances — rather than reproducing this RAM-driven shortcut in the cloud, since the constraint that motivated it doesn't apply there.
- If local hardware changes (more RAM), splitting back into per-service containers is a `docker-compose.yml` change, not a data-model change — the databases are already correctly separated for that migration.
