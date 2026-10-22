# ADR 0007: Root Compose File for Local Platform Orchestration

**Status:** Accepted  
**Date:** 2026-09-21  
**Deciders:** Dhruv (project owner)

## Context

The repository contains six independently deployable ASP.NET Core services:

- Identity
- Profile
- Catalog
- Subscription
- Payment
- Streaming

The services also depend on local infrastructure: one shared SQL Server container,
MongoDB for Catalog, and Azurite for Streaming. RabbitMQ will be added when the
Phase 4 messaging work is implemented.

Running each container manually would require repeating image, port, network,
volume, and environment configuration. It would also make service-to-service
networking depend on host-specific `localhost` configuration.

The repository roadmap defines Phase 2 as a cold start of the complete platform
with `docker compose up`, without manual infrastructure setup.

## Decision

Create the main Compose file at the repository root:

```text
netflix-clone/docker-compose.yml
```

The root Compose file is the local orchestration boundary. It will define:

- All six API services and their Docker build contexts.
- SQL Server, MongoDB, and Azurite containers.
- RabbitMQ when messaging is implemented.
- A shared Docker network for container-name service discovery.
- Persistent volumes for stateful infrastructure.
- Environment variables and connection strings supplied to the services.
- Service health checks and startup dependencies where required.

The service Dockerfiles remain inside their respective service directories because
they own the build and runtime definition for one API. Compose coordinates those
images; it does not replace the Dockerfiles.

A service-level Compose file may exist temporarily for isolated development, such
as running Profile alone. Such a file is optional and must not replace the root
Compose file or duplicate the complete platform configuration.

Kubernetes manifests remain under `k8s/`. Kubernetes is the later orchestration
stage and uses Deployments, Services, ConfigMaps, Secrets, and probes rather than
Compose service definitions.

## Consequences

### Positive

- The complete local platform has one documented startup command:
  `docker compose up -d`.
- Container-to-container communication can use stable service names instead of
  host-specific `localhost` addresses.
- Infrastructure setup, volumes, ports, and environment configuration are
  versioned alongside the application.
- The Compose topology provides a practical stepping stone toward Kubernetes.
- Individual Dockerfiles remain simple and independently buildable.

### Negative / trade-offs

- A full startup consumes more memory than running one API, which matters under
  the repository's 8GB development constraint.
- Compose is intended for local development and learning, not production
  orchestration or high availability.
- The root file grows as RabbitMQ, gateway routing, and additional infrastructure
  are introduced.
- Service-level Compose files can drift from the root configuration if they are
  maintained without a clear isolated-development purpose.

## Alternatives considered

### No Compose file

Rejected. Manual `docker run` commands do not provide a reproducible complete
platform startup and require repeated network, volume, and environment setup.

### One Compose file inside each service directory

Rejected as the primary approach. This keeps isolated development convenient but
duplicates shared infrastructure and cannot start the complete platform reliably
from one command.

### Compose file under `services/`

Rejected. The Compose file coordinates more than the services directory: it also
owns SQL Server, MongoDB, Azurite, future RabbitMQ, networks, volumes, and shared
configuration. The repository root is the clearer application boundary and matches
the documented layout.

### Compose files under `k8s/`

Rejected. `k8s/` is reserved for Kubernetes manifests. Mixing the two orchestration
formats would obscure which deployment target a file supports.

### Kubernetes as the first local orchestrator

Deferred. Kubernetes is the planned Phase 5 learning target. Compose is the simpler
Phase 2 tool for learning container images, networking, volumes, and dependencies
before introducing Kubernetes concepts.

## Revisit triggers

Revisit this decision if:

- The local stack no longer fits the available memory budget.
- A production deployment target requires a different local workflow.
- The project stops using Docker Compose as the Phase 2 learning checkpoint.
- The service count or infrastructure topology changes enough to justify separate
  development and test Compose overrides.
