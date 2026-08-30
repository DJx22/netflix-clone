# Netflix-Style Microservices — Learning Project

Backend-only, API-first, 6-service microservices build used to learn Docker, Podman,
Kubernetes, and Jenkins from zero. No frontend — every service is verified through
Postman/Swagger.

Full plan: [`docs/roadmap.md`](docs/roadmap.md). Code-level conventions:
[`csharp-coding-standard.md`](csharp-coding-standard.md).

## Status: Phase 0 — Foundations, complete

- [x] OpenAPI spec per service — `services/*/openapi.yaml`
- [x] `scripts/new-service.ps1` — scaffolds the Api/Application/Domain/Infrastructure/Tests layout
- [x] ADR 0001 — single SQL Server container, one database per service
- [x] ADR 0002 — Azurite instead of real Azure Blob Storage

**Next:** run `scripts/new-service.ps1 -ServiceName <Name>` once per service to
generate the actual project structure, then start Phase 1 (`docs/roadmap.md` §7–8).

This zip is a plain file tree, not a git repo yet — `git init`, `git add .`, and an
initial commit are still yours to run, under your own identity, not a synthetic one.

## Layout

| Path | Contents |
|---|---|
| `services/*/openapi.yaml` | API contract, written before code |
| `docs/adr/` | Decisions that diverge from a "textbook" build, and why |
| `docs/roadmap.md` | Phased plan, tool order, scope guardrails |
| `scripts/new-service.ps1` | Service scaffold generator |
| `gateway/`, `k8s/`, `jenkins/` | Empty on purpose — reserved from Phase 4, 5, and 6 respectively, so the layout is locked in now instead of drifting later |

## Prerequisites

See `docs/roadmap.md` §12.
