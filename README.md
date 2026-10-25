# Netflix-Style Microservices — Learning Project

Backend-only, API-first, 6-service microservices build used to learn Docker,
Kubernetes, and Jenkins from zero. Podman parity is deferred. No frontend — every
service is verified through Postman/Swagger.

Full plan: [`docs/roadmap.md`](docs/roadmap.md). Code-level conventions:
[`csharp-coding-standard.md`](csharp-coding-standard.md).

---

## ✅ Phase 1 — Core services, complete

All six services are implemented as plain ASP.NET Core Web APIs (.NET 10) running
locally, no Docker yet. The full sign-up → browse catalog → subscribe → stream flow
is exercisable through Swagger or Postman.

### What was built

| Service | Port | Stack | Highlights |
|---|---|---|---|
| **Identity** | `5001` | SQL Server (`IdentityDb`) | Register/login, hand-rolled JWT issuance + refresh tokens, BCrypt password hashing |
| **Profile** | `5002` | SQL Server (`ProfileDb`) | Per-account viewer profiles, plain CRUD |
| **Catalog** | `5005` | MongoDB (`CatalogDb`) | Movie/show metadata, genres, search; the only service using MediatR + CQRS |
| **Subscription** | `5003` | SQL Server (`SubscriptionDb`) | Plans, plan changes, cancellation; consumes `PaymentCompleted` event (RabbitMQ — Phase 4) |
| **Payment** | `5004` | SQL Server (`PaymentDb`) | In-process mock billing (random success/fail); emits `PaymentCompleted` |
| **Streaming** | `5006` | SQL Server (`StreamingDb`) + Azurite (blobs) | Media-asset lookup, playback-position tracking (upsert), post-miss Catalog diagnostic |

### Architecture

- **Clean Architecture (Onion)** — `Domain → Application → Infrastructure → Api` dependency
  direction on every service; no layer may import from the layer above it.
- **No CQRS outside Catalog.** Identity, Profile, Subscription, Payment, and Streaming
  are plain-CRUD services — no command/query split, no MediatR.
- **Typed results over exception-flow** for expected not-found paths (`GetMediaResult`,
  `GetPositionResult`) — controllers switch on the result type, not catch blocks.
- **FluentValidation** on every incoming request DTO.
- **Serilog** structured logging with `X-Correlation-Id` propagated through middleware
  and included on every log line and every `ProblemDetails` error response.
- **JWT Bearer auth** on all write/user-scoped endpoints; health endpoints are anonymous.
- **EF Core 10** (Identity, Profile, Subscription, Payment, Streaming) with explicit
  migrations checked in under each service's `Streaming.Infrastructure/Migrations/`.
- **MongoDB driver** (Catalog) with `BsonClassMap` configuration in Infrastructure;
  no driver types in Domain or Application.

### Test suite

Every service has a `*.Tests` project:
- **Unit tests** — application-service logic mocked with Moq + FluentAssertions.
- **Validator tests** — FluentValidation rules exercised with `TestValidate`.
- **Infrastructure integration tests** — real SQL Server (or MongoDB) via Testcontainers;
  no mocks at this layer.
- **HTTP client tests** — `CatalogHttpClient` tested with fake `HttpMessageHandler`s;
  no network calls.

### Key docs produced

| File | Purpose |
|---|---|
| [`docs/adr/`](docs/adr/) | All architecture decisions (SQL Server consolidation, Azurite, Streaming data store, …) |
| [`docs/roadmap.md`](docs/roadmap.md) | Full phased plan |
| [`docs/backlogs/streaming-service-deferrals.md`](docs/backlogs/streaming-service-deferrals.md) | Known Phase 1–3 deferrals with explicit phase-trigger criteria |
| `services/streaming-service/docs/backlog.md` | Streaming-specific code-level flags (EF10 syntax, concurrent upsert race) |

---

## ✅ Phase 2 — Dockerize, complete

All six services are containerized. The full platform (services + SQL Server + MongoDB +
Azurite + RabbitMQ) starts from cold with a single command and all containers reach
healthy status with no manual steps.

### What was built

| Artifact | Details |
|---|---|
| **Dockerfiles** | One multi-stage Dockerfile per service (`Dockerfile.<service>.service`). SDK image compiles and publishes; runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) carries only the published output. |
| **`docker-compose.yml`** | Root compose file: all 6 service containers + SQL Server + MongoDB + Azurite + RabbitMQ on a shared `netflix-clone-net` bridge network. |
| **`docker-compose.dev.yml`** | Dev override: host-port mappings (5001–5006, 1433, 27017, 10000–10002, 5672/15672), named volumes for SQL/Mongo/Azurite data, EF migration init containers. |
| **Healthchecks** | Every service container has a `HEALTHCHECK` using `curl /health`. Infrastructure containers (SQL Server, MongoDB, RabbitMQ) have healthchecks; service containers `depends_on: condition: service_healthy`. |
| **`StreamingDb.MediaAssets` admin API** | `POST /api/v1/admin/media-assets` added — no longer requires a raw SQL INSERT to seed media assets. Upsert semantics: re-registering an existing `titleId` updates the record. |
| **Environment wiring** | `CatalogClient:BaseUrl` (→ `CatalogClient__BaseUrl`) injected via compose env for the Streaming → Catalog diagnostic fallback call. |

### Compose quick-start

```powershell
cd <repo-root>
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build

# Check all containers are healthy
docker compose -f docker-compose.yml -f docker-compose.dev.yml ps

# Tear down (preserves volumes by default)
docker compose -f docker-compose.yml -f docker-compose.dev.yml down

# Full teardown including volumes
docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v
```

### Individual service image builds

Each image can also be built and smoke-tested independently:

```powershell
cd services/identity-service
docker build -f Dockerfile.identity.service -t identity-service:local .
docker run --rm -p 5001:5001 identity-service:local
```

> See [`docs/docker-setup/README.md`](docs/docker-setup/README.md) for the full Docker
> setup guide, including the WSL2 memory cap recommendation.

---

## ✅ Phase 4 — RabbitMQ event-driven activation, complete

Payment no longer calls Subscription over HTTP. A successful payment publishes a
`PaymentCompleted` event to RabbitMQ; the Subscription service activates the
subscription in-process via a hosted consumer.

### What changed

| Area | Change |
|---|---|
| **Payment → `RabbitMqEventPublisher`** | Replaced `SubscriptionActivationClient` (HTTP bridge). Publishes `PaymentCompleted` to `payment.events` exchange on success only. |
| **Subscription → `PaymentCompletedConsumer`** | New `BackgroundService`. Declares topology on startup, acks only after DB write, nacks to DLQ on unprocessable messages. |
| **DLQ topology** | `payment.events.dlx` (fanout) → `subscription.payment-completed.dlq`. Malformed JSON, unknown subscription IDs, and unexpected errors go here. |
| **Correlation ID** | `X-Correlation-Id` flows from the HTTP request → AMQP header → Subscription log line. End-to-end traceable. |
| **HTTP bridge removed** | `SubscriptionActivationClient.cs` deleted. No Polly pipeline, no `SubscriptionService__BaseUrl` env var. |
| **Compose dependency** | `payment-service` no longer `depends_on` `subscription-service`. Both depend on `rabbitmq: condition: service_healthy`. |
| **Streaming hardening** | `PlaybackPositionRepository.UpsertAsync` now catches SQL 2627/2601 concurrent-insert races and converts them to UPDATE (§ConcurrentUpsertRace resolved). |

### Phase 4 async contract

> **Activation is now asynchronous.** After `POST /api/v1/payments` returns `201`, the
> subscription may still be in `PendingPayment` status for a brief window while the
> `PaymentCompleted` event travels through RabbitMQ to the Subscription consumer.
> Postman tests that check subscription status immediately after payment should include
> a short wait (≈ 1–2 s) before the status-check request.

### RabbitMQ management UI

After `docker compose up`, the management UI is reachable at:

```
http://localhost:15672   (guest / guest)
```

Queues to inspect:
- `subscription.payment-completed` — active messages, consumer count, ack rate
- `subscription.payment-completed.dlq` — dead-letter messages (malformed / unprocessable)

### Compose workflow (updated)

```powershell
# Development stack (RabbitMQ now included in root compose)
cd <repo-root>
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build

# Inspect health/status
docker compose -f docker-compose.yml -f docker-compose.dev.yml ps

# Tear down everything (including rabbitmq-data volume)
docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v
```

> **Note:** `rabbitmq` must reach `service_healthy` before `payment-service` and
> `subscription-service` start. The healthcheck uses `rabbitmq-diagnostics ping`
> (interval 10 s, 12 retries, 20 s start period).

The Docker setup guide is here: [`docs/docker-setup/README.md`](docs/docker-setup/README.md).

---

## ▶ Running locally (Phase 1 — no Docker)

Each service runs under its `local` launch profile. Open one terminal per service:

```powershell
cd services/identity-service/Identity.Api       && dotnet run --launch-profile local
cd services/profile-service/Profile.Api         && dotnet run --launch-profile local
cd services/catalog-service/Catalog.Api         && dotnet run --launch-profile local
cd services/subscription-service/Subscription.Api && dotnet run --launch-profile local
cd services/payment-service/Payment.Api         && dotnet run --launch-profile local
cd services/streaming-service/Streaming.Api     && dotnet run --launch-profile local
```

Swagger UI is available at `http://localhost:{port}/swagger` on each service.

Infrastructure must be running before the services:

```powershell
# SQL Server (single shared instance, five logical databases)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStr0ngPassw0rd!" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest

# MongoDB (Catalog only)
docker run -p 27017:27017 --name mongodb -d mongo:7

# Azurite (Streaming blob storage)
azurite --silent --location ./azurite-data
```

Apply EF Core migrations (one-off, or after a schema change):

```powershell
# Run from each Infrastructure project directory
dotnet ef database update
```

---

## 🗄 Manual seeding — where data must be inserted by hand

Some stores are read-only from the API surface and must be seeded directly.

### 1. Catalog — titles (MongoDB)

Catalog has a `POST /api/v1/titles` endpoint — **no direct DB insert needed**.
Create titles through the API (requires a JWT):

```bash
TOKEN=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Password1!"}' | jq -r '.accessToken')

TITLE_ID=$(curl -s -X POST http://localhost:5005/api/v1/titles \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Inception",
    "genres": ["Sci-Fi", "Thriller"],
    "releaseYear": 2010,
    "maturityRating": "PG-13",
    "durationMinutes": 148,
    "streamingAssetId": "inception.mp4"
  }' | jq -r '.titleId')

echo "Catalog titleId: $TITLE_ID"   # e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

> **Keep the `titleId` Catalog returns.** It is a GUID generated internally and is
> the shared key between Catalog and Streaming. Do not invent your own slug.

### 2. Streaming — `MediaAssets` (Phase 2+: admin API endpoint)

**Phase 2 and later** — use the admin API endpoint (no direct SQL insert needed):

```bash
curl -s -X POST http://localhost:5006/api/v1/admin/media-assets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "titleId": "'"$TITLE_ID"'",
    "mediaUrl": "http://127.0.0.1:10000/devstoreaccount1/media/inception.mp4",
    "contentType": "video/mp4",
    "durationSeconds": 8880
  }'
```

The endpoint is idempotent (upsert) — re-posting with the same `titleId` updates the record.

**Phase 1 only** — seed directly with a SQL INSERT:

```sql
-- Run against StreamingDb (sqlcmd or SSMS)
INSERT INTO MediaAssets (TitleId, MediaUrl, ContentType, DurationSeconds)
VALUES (
  '3fa85f64-5717-4562-b3fc-2c963f66afa6',   -- GUID from Catalog POST response
  'http://127.0.0.1:10000/devstoreaccount1/media/inception.mp4',
  'video/mp4',
  8880
);
```

> See [`docs/backlogs/streaming-service-deferrals.md §TitleIdContractAlignment`](docs/backlogs/streaming-service-deferrals.md)
> for why this must be the Catalog GUID, not a custom slug.

### 3. Subscription plans (SQL Server `SubscriptionDb`)

Plans are seeded by an EF migration when the subscription database is first created in Development or Testing. The database stores prices as `PriceMonthly`; the API treats them as the default currency (USD).

### 4. Identity — accounts

Created through the API — no manual insert needed:

```bash
curl -s -X POST http://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Password1!","username":"testuser"}'
```

### 5. Everything else

| Store | Seeded via |
|---|---|
| `IdentityDb.Accounts` | `POST /api/v1/auth/register` |
| `ProfileDb.Profiles` | `POST /api/v1/profiles` (after login) |
| `CatalogDb.titles` | `POST /api/v1/titles` (after login) |
| `SubscriptionDb.Plans` | Seeded once by the subscription database migrations |
| `SubscriptionDb.Subscriptions` | `POST /api/v1/subscriptions` (after login + plan seeded) |
| `PaymentDb.Payments` | Created internally when subscription is created |
| `StreamingDb.MediaAssets` | SQL INSERT (step 2 above) |
| `StreamingDb.PlaybackPositions` | `PUT /api/v1/playback/{titleId}/position` (after login) |

---

## ⏭️ Phase 5 — Kubernetes (Kind) — next up

Kubernetes manifests and a local Kind cluster. Docker Compose remains the local
development workflow; Kubernetes targets CI / staging verification.

Goal: kill a pod, watch it self-heal; `kubectl scale` works; the full stack fits inside
the 8GB budget. See [`docs/roadmap.md §8`](docs/roadmap.md) for phase detail.

---

## ⏭️ Phase 6 — Jenkins CI/CD — planned

Jenkins in a container (started manually per session). Pipeline: restore → build → test →
`docker build` → push to Docker Hub → `kubectl apply`. The `scripts/ci-test.sh` script
lays the groundwork for steps 1–3 of this pipeline.

---

## Layout

| Path | Contents |
|---|---|
| `services/*/openapi.yaml` | API contract, written before code |
| `services/*/*.Domain/` | Entities, value objects, repository interfaces |
| `services/*/*.Application/` | Use-case services, DTOs, validators, result types |
| `services/*/*.Infrastructure/` | EF Core / MongoDB, repository implementations, external HTTP clients |
| `services/*/*.Api/` | Controllers, middleware, DI wiring, `Program.cs` |
| `services/*/*.Tests/` | Unit, validator, and integration tests |
| `services/*/Dockerfile.<name>.service` | Multi-stage Dockerfile per service |
| `docker-compose.yml` | Root compose: all services + SQL Server + MongoDB + Azurite + RabbitMQ |
| `docker-compose.dev.yml` | Dev override: host ports, named volumes, EF migration init containers |
| `postman/` | Postman collection + environment file for the full API suite |
| `docs/adr/` | Architecture decision records |
| `docs/roadmap.md` | Phased plan, tool order, scope guardrails |
| `docs/backlogs/` | Phase-deferred decisions with explicit trigger criteria |
| `k8s/` | Reserved for Phase 5 — Kubernetes manifests |
| `jenkins/` | Reserved for Phase 6 — Jenkinsfile |
