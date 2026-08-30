# Netflix-Style Microservices Learning Roadmap

A self-study build to learn microservices architecture, Docker, Podman, Kubernetes, and Jenkins — using a Netflix-style streaming platform as the vehicle, not the goal. This is a revision of an earlier draft plan, rescoped against your actual constraints (zero prior tool experience, 8GB RAM, 5–10 hrs/week, no frontend).

## 0. Ground rules

- **6 services are the vehicle. Docker → Podman → Kubernetes → Jenkins, in that order, is the point.** Don't let service count creep before the pipeline is proven.
- **100% backend, API-first.** No frontend project, no UI service. Every checkpoint below is verified through Postman/Swagger — same as your internship project.
- **`csharp-coding-standard.md` (already in this project) is the code-level baseline.** This roadmap only adds what that standard doesn't cover: containerization, orchestration, CI/CD, and inter-service communication. It is not repeated here.
- **8GB RAM is the real constraint on this build — not CPU, not time.** Several choices below exist specifically to fit that budget. They're called out, not hidden, so you know what you're trading off and can undo it later on better hardware if you want to.

## 1. Locked decisions from scoping

| Question | Decision |
|---|---|
| Prior tool experience | None — Docker, Podman, Kubernetes, Jenkins all learned from zero |
| Monolith-first? | No — 6 separate service projects from day one |
| Core service count | 6 |
| Frontend | None — API-only via Postman/Swagger |
| Messaging | RabbitMQ included in the core phases, not deferred to expansion |
| OS | Windows 11 + WSL2 |
| Hardware | 8GB RAM, i5-1240P (12c/16t) — CPU is not the bottleneck, RAM is |
| Time budget | 5–10 hrs/week |
| Jenkins hosting | Local container, started only when actively practicing CI/CD |

## 2. Service inventory

**Core — build only these until Phase 8:**

| # | Service | Responsibility | Data store | Notes |
|---|---|---|---|---|
| 1 | Identity | Register/login, JWT issuance, refresh tokens | SQL Server (shared instance, own DB) | Roll your own JWT — no Keycloak/Auth0 |
| 2 | Profile | Per-account viewer profiles, avatars, preferences | SQL Server (shared instance, own DB) | Plain CRUD, no CQRS — no real variation to abstract over |
| 3 | Catalog | Movie/show metadata, genres, search | MongoDB | The one service using Mediator/CQRS — heavy reads, occasional writes, per your own standards doc §6 |
| 4 | Subscription | Plans, entitlements, plan changes | SQL Server (shared instance, own DB) | Consumes `PaymentCompleted` event via RabbitMQ |
| 5 | Payment | Mock billing, invoices | SQL Server (shared instance, own DB) | In-process mock (random success/fail), **not** real Stripe test mode — one less external account/network dependency to manage alongside everything else |
| 6 | Streaming/Playback | Serves manifests/chunks, tracks position | Azurite (local Blob emulator) + Redis | Real Azure Blob Storage only if/when you do Phase 9 |

**Expansion (Phase 8, don't build yet):** Recommendation, Watch History, Notification, Search, Reviews/Ratings, Admin/CMS.

**"One database per service" still holds** even though four services share one SQL Server *container* — each gets its own logical database, no service queries another's DB directly. What you're saving is four separate SQL Server engine processes (~1.4GB+ each), not the isolation itself.

## 3. Tech stack — and why it diverges from a "textbook" build

| Layer | Choice | Reasoning |
|---|---|---|
| Framework | ASP.NET Core, .NET 10 (LTS) | .NET 8 goes end-of-support Nov 2026 — starting a multi-month project on it now just guarantees a mid-project migration |
| Relational data | One SQL Server 2022 container, 4 logical DBs | RAM budget — see §2 |
| Document data | MongoDB, own container | Catalog only |
| Cache | Redis, own container | Playback-position cache |
| Blob storage | Azurite (emulator), own container | Avoids an Azure account/cost dependency for a phase that doesn't need one |
| Messaging | RabbitMQ, own container, raw `RabbitMQ.Client` | MassTransit hides the exact mechanics you're trying to learn — revisit it later, once you know what it wraps |
| Gateway | YARP | .NET-native, actively maintained by Microsoft — one less unfamiliar stack on top of everything else new |
| Containers | Docker Desktop (WSL2 backend), then Podman | See §5 for the Windows-specific caveat on what Podman actually teaches you here |
| Orchestration | Kind, single node | Runs cluster nodes as containers inside the Docker VM you already have — Minikube's default driver adds a second VM layer on top of that, for no benefit here |
| CI/CD | Jenkins, containerized, on-demand | Started manually before a session, stopped after — not a background service |
| Registry | Docker Hub, public repos, free tier | No Azure budget confirmed; revisit ACR only if Phase 9 happens |
| Observability | `/health` → K8s probes (Phase 5+); Serilog + Seq (Phase 7) | Prometheus/Grafana stays a stretch goal, not core |

## 4. Tool learning order — the actual dependency chain

**Docker → Podman → Kubernetes → Jenkins.**

- **Docker** has no prerequisite among these four — everything after it needs a container to act on.
- **Podman** doesn't technically depend on Docker (separate runtime, same OCI image format), but it only teaches you anything as a point of *comparison* — so it goes right after Docker, not before.
- **Kubernetes** needs images to schedule — it depends on Docker (or Podman) having produced something first.
- **Jenkins'** real pipeline (build → test → containerize → push → deploy) depends on both Docker (to containerize) and Kubernetes (to have a deploy target). A Jenkins pipeline that only builds and tests could run earlier, but that's not the CI/CD you asked to learn.
- **RabbitMQ** sits outside this chain — it's an application-level dependency, not infra tooling with a sequencing requirement. It lands in Phase 4 because that's the natural point (Docker already understood, K8s not yet needed to run it), not because it depends on anything above it.

## 5. Windows / WSL2 — read before Phase 2

- **Cap WSL2's memory explicitly.** Create/edit `%UserProfile%\.wslconfig`:
  ```ini
  [wsl2]
  memory=6GB
  processors=8
  ```
  Without this, WSL2 claims RAM on demand and starves Windows itself before Docker is even the bottleneck.
- **Podman on Windows won't teach you what it teaches on native Linux.** Its real selling points — rootless containers, no background daemon — depend on talking to the Linux kernel directly. On Windows, Podman runs its own Linux VM (`podman machine`) via WSL2, same as Docker Desktop does. You'll still learn the CLI differences and `podman play kube`, but "daemonless" and "rootless" will be true of the VM, not something you can feel the way you could on bare-metal Linux. Say that plainly when you document it — don't write up a comparison you didn't actually get to make.

## 6. Repo layout

```
netflix-clone/
├── services/
│   ├── identity-service/
│   ├── profile-service/
│   ├── catalog-service/
│   ├── subscription-service/
│   ├── payment-service/
│   └── streaming-service/
├── gateway/
├── k8s/
│   ├── base/
│   └── overlays/{dev}/
├── jenkins/
│   └── Jenkinsfile.template
├── scripts/
│   └── new-service.ps1        # scaffolds Api/Application/Domain/Infrastructure/Tests in one shot
├── docker-compose.yml
└── docs/
    └── adr/                    # architecture decision records
```

No `frontend/` directory — there isn't one.

## 7. Phased roadmap

| Phase | Focus | Est. duration @ 5–10 hrs/wk | Done when |
|---|---|---|---|
| 0 | Foundations | 1–2 weeks | OpenAPI spec written per service; `new-service` scaffold script works |
| 1 | Core services, no containers | 5–7 weeks | Register → profile → pick plan → pay → browse catalog, all via Swagger, no containers involved |
| 2 | Dockerize | 3–4 weeks | `docker compose up` brings up all 6 services + SQL Server + MongoDB + Redis + Azurite from cold, zero manual steps |
| 3 | Podman parity | 1 week | Same stack under `podman-compose`; you can name 2–3 real differences you personally hit, not textbook ones |
| 4 | Gateway + RabbitMQ | 3–4 weeks | Postman only ever calls the gateway; `PaymentCompleted` → `SubscriptionActivated` happens with zero direct HTTP call between those two services |
| 5 | Kubernetes (local) | 5–7 weeks | Kill a pod, watch it self-heal; `kubectl scale` works; the full stack fits and runs inside your 8GB budget |
| 6 | Jenkins CI/CD | 3–4 weeks | `git push` → a manually-started Jenkins container runs build → test → containerize → push → deploy with no manual steps in between |
| 7 | Observability & resilience | 2–3 weeks | A killed dependency degrades gracefully (Polly) instead of cascading; a correlation ID is traceable end-to-end in Seq |
| 8 | Expansion services | Open-ended | Add from the expansion list once Phases 0–7 are solid |
| 9 | Cloud stretch | Optional, unscheduled | Same manifests on AKS — see open items below |

**Phases 0–7 total: roughly 23–32 weeks — call it 6–7 months at 5–10 hrs/week.** That's a real estimate, not a rounded-down one — it assumes zero prior exposure to Docker, Podman, Kubernetes, or Jenkins, which is what you told me. Phase 1 (six services' worth of boilerplate) and Phase 5 (Kubernetes) are where this is most likely to run long; budget slack there first.

## 8. Phase detail

**Phase 0 — Foundations**
- OpenAPI spec per service, written before any code
- Build the `new-service` scaffold script once (Api/Application/Domain/Infrastructure/Tests projects + references) — six services should not mean six hours of manual `dotnet new` and manual project-reference wiring
- Write the SQL Server consolidation and Azurite-instead-of-real-Blob decisions as short ADRs so you're not re-litigating them mid-build

**Phase 1 — Core services, no containers**
- Plain ASP.NET Core Web APIs over HTTP, no Docker yet
- Full "sign up → browse catalog" flow working through Postman/Swagger before touching a Dockerfile
- Catalog gets Mediator/CQRS; everything else stays plain CRUD

**Phase 2 — Dockerize**
- One multi-stage Dockerfile per service (SDK image → runtime image)
- `docker-compose.yml` wiring all 6 services + the single SQL Server container + MongoDB + Redis + Azurite
- First real contact with the WSL2 memory cap from §5 — expect to hit it

**Phase 3 — Podman parity**
- `podman-compose up` (or `podman play kube` after a first-draft K8s YAML)
- Document what's actually different on Windows specifically — see the caveat in §5 before you write this up

**Phase 4 — Gateway & RabbitMQ**
- YARP in front of every service; Postman never calls a service directly again
- RabbitMQ container; `PaymentCompleted → SubscriptionActivated` as your first real async, event-driven hop, using `RabbitMQ.Client` directly
- This is a genuine new conceptual domain (exchanges, queues, bindings, consumers) — don't compress it just because it's "only messaging"

**Phase 5 — Kubernetes, local**
- Generate a first-draft manifest with `kompose convert`, then rewrite it by hand until you understand every line — Deployment, Service, ConfigMap, Secret, Ingress
- Single-node Kind cluster
- This is the phase most likely to collide with your 8GB budget — expect to run a subset of services at a time rather than the full stack while learning

**Phase 6 — Jenkins CI/CD**
- Jenkins in a container, started manually for each session
- Pipeline: restore/build/test → `docker build` → push to Docker Hub → deploy (`kubectl apply`)

**Phase 7 — Observability & resilience**
- `/health` endpoints wired to K8s liveness/readiness probes
- Serilog + Seq, correlation ID propagated across every service call
- Polly retries/circuit breakers on inter-service HTTP calls

**Phase 8 — Expansion**
- Add Recommendation, Watch History, Notification, etc. using the now-proven pipeline — should be fast, since the infra work is already done

**Phase 9 — Cloud stretch (optional)**
- Same manifests → AKS, ACR, Key Vault — genuinely optional, and gated on the open item below

## 9. Scope guardrails

- **Streaming ≠ video engineering.** No live transcoding. Encode a handful of sample clips once with ffmpeg, store in Azurite, serve via HTTP range requests. The lesson is service design, not codec work.
- **Payment is a mock, not a Stripe integration.** In-process success/fail simulation — the point is the event it emits, not third-party API mechanics.
- **Auth: your own JWT issuance.** Keycloak/Auth0 is a deliberate later exercise, not a Phase 0 dependency.
- **CQRS lives in Catalog only.** Applying it everywhere adds ceremony without teaching you anything new twice.

## 10. Weekly checkpoint habit

A log only works as accountability if something is actually watching it — a private file nobody opens is documentation, not a forcing function. Pick one:

- **Public with real readers**: a weekly post somewhere with actual traffic — a subreddit (r/dotnet, r/Kubernetes), a Discord/Slack study group, or LinkedIn. Silence is visible there in a way a private repo never makes it.
- **Public but low-traffic**: a `DEVLOG.md` in the repo, linked from the README, one dated entry per week — only works as accountability if you also name one specific person who'll spot-check it occasionally. Without that person, it defaults back to plain documentation.

Same format and cadence either way:

- **Cadence**: same day every week — Sunday evening works well, since it reflects the week just finished and sets up the next one before Monday. A missed entry is the actual signal to watch for, more than anything written in the entries themselves.
- **Entry template** (keep each one under 5 lines):
  ```
  ## Week of <date>
  Done: <what actually shipped — not "worked on X">
  Blocked: <what stopped you, if anything>
  Next: <one concrete thing for next week>
  Phase: <current phase from §7 — on track / N weeks behind estimate>
  ```
- **Tie it to §7, not vibes.** "On track for Phase 2" or "3 weeks behind the Phase 1 estimate" is a real data point against the phase table above. "Made progress" is not — don't let entries drift into that.

## 11. Open items — not resolved, don't treat this file as if they are

- **Phase 9 / Azure budget.** Cloud stretch needs either your own Azure subscription or a decision to skip it outright. Not decided.
- **Log venue.** §10 gives you the two shapes an accountability habit can take; which specific place (subreddit, Discord, a named person) is still yours to pick.
- **RabbitMQ scope.** "Included in core phases" is decided; exact depth isn't — a working producer/consumer pair is very different from adding dead-letter queues and retry topology. Worth pinning down when you're actually in Phase 4, not now.

## 12. Prerequisites checklist

- [ ] .NET 10 SDK
- [ ] Docker Desktop (WSL2 backend)
- [ ] `.wslconfig` memory cap set (§5)
- [ ] Podman Desktop + podman-compose
- [ ] kubectl
- [ ] Kind
- [ ] Jenkins (container image pulled, not running by default)
- [ ] Postman
- [ ] Docker Hub account (public repos)

## 13. Working with your AI IDE

- Scaffold one service per prompt, not the whole platform at once — feed it that service's OpenAPI contract plus `csharp-coding-standard.md` as context
- For Dockerfiles and K8s manifests specifically, ask it to explain each line back to you before accepting — the point is understanding, not just working YAML
- Keep `docs/adr/` as short decision-record `.md` files — re-feed these in later sessions so the AI doesn't re-litigate settled decisions (SQL Server consolidation, Azurite over real Blob, etc.)
