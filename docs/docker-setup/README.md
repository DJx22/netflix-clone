# Docker Setup

This repository uses Docker Compose to run the six API services and their local
infrastructure together.

## Prerequisites

Install and start Docker Desktop with the Linux container engine enabled. From the
repository root, verify Docker is available:

```powershell
docker version
docker compose version
```

The default SQL Server password is `YourStr0ngPassw0rd!`. Override it before
starting the stack when required:

```powershell
$env:SA_PASSWORD = "A_Stronger_Password1!"
```

The password must satisfy SQL Server password requirements. The same value is used
by SQL Server and the API connection strings.

## Compose files

| File | Purpose |
|---|---|
| `docker-compose.yml` | Base service and infrastructure definitions |
| `docker-compose.dev.yml` | Normal development environment |
| `docker-compose.test.yml` | Isolated QA/test environment |
| `docker-compose.prod.yml` | Production-shaped configuration using published images |

The service Dockerfiles remain under their service directories:

```text
services/<service>/Dockerfile.<service>.service
```

## Docker files created

The Docker setup consists of one Dockerfile and one `.dockerignore` file per API,
plus four root-level Compose files.

### API Dockerfiles

| Service | Dockerfile | Application project | Entry point |
|---|---|---|---|
| Identity | `services/identity-service/Dockerfile.identity.service` | `Identity.Api/Identity.Api.csproj` | `Identity.Api.dll` |
| Profile | `services/profile-service/Dockerfile.profile.service` | `Profile.Api/Profile.Api.csproj` | `Profile.Api.dll` |
| Catalog | `services/catalog-service/Dockerfile.catalog.service` | `Catalog.Api/Catalog.Api.csproj` | `Catalog.Api.dll` |
| Subscription | `services/subscription-service/Dockerfile.subscription.service` | `Subscription.Api/Subscription.Api.csproj` | `Subscription.Api.dll` |
| Payment | `services/payment-service/Dockerfile.payment.service` | `Payment.Api/Payment.Api.csproj` | `Payment.Api.dll` |
| Streaming | `services/streaming-service/Dockerfile.streaming.service` | `Streaming.Api/Streaming.Api.csproj` | `Streaming.Api.dll` |

### API ignore files

The six service ignore files are intentionally identical:

```text
services/identity-service/.dockerignore
services/profile-service/.dockerignore
services/catalog-service/.dockerignore
services/subscription-service/.dockerignore
services/payment-service/.dockerignore
services/streaming-service/.dockerignore
```

They exclude `bin`, `obj`, `.http`, and Swagger `*.yaml` files. This prevents host
build output, Windows-specific NuGet metadata, request documents, and source API
contracts from entering the Linux build context. Configuration needed by a
container is supplied through Compose environment variables rather than copied
from a developer workstation.

## Dockerfile implementation rules

Every service Dockerfile follows the same two-stage structure:

1. Use `mcr.microsoft.com/dotnet/sdk:10.0` as the build image.
2. Set the build working directory to `/src`.
3. Copy the API project file and every directly referenced production project file
	before copying source code, keeping restore cacheable.
4. Run `dotnet restore` against the API project.
5. Copy the remaining service source.
6. Run `dotnet publish` in `Release` configuration with `--no-restore` to
	`/app/publish`.
7. Use `mcr.microsoft.com/dotnet/aspnet:10.0` as the final runtime image.
8. Set the runtime working directory to `/app` and copy only published output.
9. Set `ASPNETCORE_URLS` to the service port and declare the same port with
	`EXPOSE`.
10. Start the published application directly with `ENTRYPOINT`, for example:
	 `dotnet Profile.Api.dll`.

The runtime image does not contain the SDK, source tree, tests, or development
launch profile.

## Development

Development is the normal workflow and builds the local images from source:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

Check the containers:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml ps
```

Follow all logs or one service:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f identity-service
```

Stop the containers without deleting persistent data:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml down
```

Remove the containers and development volumes when a clean local reset is needed:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v
```

## Service endpoints

| Service | URL |
|---|---|
| Identity | `http://localhost:5001/swagger` |
| Profile | `http://localhost:5002/swagger` |
| Subscription | `http://localhost:5003/swagger` |
| Payment | `http://localhost:5004/swagger` |
| Catalog | `http://localhost:5005/swagger` |
| Streaming | `http://localhost:5006/swagger` |

Infrastructure is exposed for local diagnostics on these ports:

- SQL Server: `localhost:1433`
- MongoDB: `localhost:27017`
- Azurite Blob: `localhost:10000`

Inside the Compose network, services use container names rather than `localhost`:

- SQL Server: `sqlserver:1433`
- MongoDB: `mongodb:27017`
- Catalog API: `http://catalog-service:5005`
- Subscription API: `http://subscription-service:5003`
- Azurite Blob: `http://azurite:10000`

## QA/test environment

The test overlay uses the same service images and real local infrastructure, but
separates test data from development data:

- SQL databases use `_Test` names.
- MongoDB uses `CatalogDb_Test`.
- SQL Server, MongoDB, and Azurite use separate named volumes.
- APIs run with `ASPNETCORE_ENVIRONMENT=Testing`.

Start it with:

```powershell
docker compose -f docker-compose.yml -f docker-compose.test.yml up -d --build
```

This is an isolated integration/QA environment, not a mock implementation. The
repository does not currently contain mock database or storage services.

Stop and remove test data:

```powershell
docker compose -f docker-compose.yml -f docker-compose.test.yml down -v
```

## Production-shaped configuration

Production is documented for future use and is not the current workflow. It does
not build from source. It expects published images supplied through environment
variables:

```powershell
$env:IDENTITY_IMAGE = "registry.example.com/netflix/identity:latest"
$env:PROFILE_IMAGE = "registry.example.com/netflix/profile:latest"
$env:CATALOG_IMAGE = "registry.example.com/netflix/catalog:latest"
$env:SUBSCRIPTION_IMAGE = "registry.example.com/netflix/subscription:latest"
$env:PAYMENT_IMAGE = "registry.example.com/netflix/payment:latest"
$env:STREAMING_IMAGE = "registry.example.com/netflix/streaming:latest"

docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Do not use the default development secrets or passwords for a real deployment.
Production secrets, registry credentials, TLS, backups, and high availability are
outside the current local Compose scope.

## Configuration overrides

Compose variables can be overridden from the shell or a root `.env` file. Useful
variables include:

- `SA_PASSWORD`
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_SECRET`
- `IDENTITY_IMAGE`
- `PROFILE_IMAGE`
- `CATALOG_IMAGE`
- `SUBSCRIPTION_IMAGE`
- `PAYMENT_IMAGE`
- `STREAMING_IMAGE`

Do not commit real credentials or production connection strings.

## Troubleshooting

Inspect the rendered configuration before starting the stack:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml config
```

If a container starts before its dependency is ready, inspect its logs and restart
the affected service:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs service-name
docker compose -f docker-compose.yml -f docker-compose.dev.yml restart service-name
```

A full reset removes all local database and blob data:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml down -v
```
