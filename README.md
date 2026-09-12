# Netflix-Style Microservices — Learning Project

Backend-only, API-first, 6-service microservices build used to learn Docker, Podman,
Kubernetes, and Jenkins from zero. No frontend — every service is verified through
Postman/Swagger.

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

### 2. Streaming — `MediaAssets` table (SQL Server `StreamingDb`)

There is no API endpoint to create media assets in Phase 1. Seed directly using the
**GUID from step 1** and the Azurite blob URL:

```bash
# First upload the file to Azurite
az storage blob upload \
  --container-name media --name inception.mp4 \
  --file "C:\path\to\inception.mp4" \
  --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;\
AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;\
BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
```

```sql
-- Run against StreamingDb (sqlcmd or SSMS)
INSERT INTO MediaAssets (TitleId, MediaUrl, ContentType, DurationSeconds)
VALUES (
  '3fa85f64-5717-4562-b3fc-2c963f66afa6',   -- GUID from Catalog POST response
  'http://127.0.0.1:10000/devstoreaccount1/media/inception.mp4',
  'video/mp4',
  8880   -- 148 min × 60
);
```

> See [`docs/backlogs/streaming-service-deferrals.md §TitleIdContractAlignment`](docs/backlogs/streaming-service-deferrals.md)
> for why this must be the Catalog GUID, not a custom slug.

### 3. Subscription plans (SQL Server `SubscriptionDb`)

Plans are reference data. Seed once:

```sql
-- Run against SubscriptionDb
INSERT INTO Plans (PlanId, Name, PriceAmount, PriceCurrency, MaxProfiles, VideoQuality)
VALUES
  ('basic',    'Basic',    9.99,  'USD', 1, 'SD'),
  ('standard', 'Standard', 14.99, 'USD', 2, 'HD'),
  ('premium',  'Premium',  19.99, 'USD', 4, 'UHD');
```

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
| `SubscriptionDb.Plans` | SQL INSERT (step 3 above) |
| `SubscriptionDb.Subscriptions` | `POST /api/v1/subscriptions` (after login + plan seeded) |
| `PaymentDb.Payments` | Created internally when subscription is created |
| `StreamingDb.MediaAssets` | SQL INSERT (step 2 above) |
| `StreamingDb.PlaybackPositions` | `PUT /api/v1/playback/{titleId}/position` (after login) |

---

## 🔜 Next — Phase 2: Dockerize

- One multi-stage `Dockerfile` per service (SDK image → runtime image).
- `docker-compose.yml` bringing all 6 services + SQL Server + MongoDB + Azurite from cold.
- First real contact with the WSL2 memory cap — see [`docs/roadmap.md §5`](docs/roadmap.md).

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
| `docs/adr/` | Architecture decision records |
| `docs/roadmap.md` | Phased plan, tool order, scope guardrails |
| `docs/backlogs/` | Phase-deferred decisions with explicit trigger criteria |
| `gateway/`, `k8s/`, `jenkins/` | Empty — reserved for Phases 4, 5, 6 |
