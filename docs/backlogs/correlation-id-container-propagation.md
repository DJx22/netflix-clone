# Backlog: Correlation ID Propagation Across Container Network Boundaries

**Status:** Open — identified during Phase 2 planning (not yet verified against actual
container-to-container traffic).

**§ tag:** `§CorrelationIdContainerPropagation`

**Phase trigger:** Verify during Phase 2, Week 3 (task W3.8 in
`phase-2-dockerize-roadmap.md`). If unverified, this becomes a hard blocker for Phase
7 observability work (Serilog + Seq, end-to-end correlation ID tracing is an explicit
Phase 7 "done when" criterion in the roadmap).

---

## Problem

Every service has `X-Correlation-Id` middleware per coding standard §13: generate one
if absent from the incoming request, carry it through `HttpContext.Items`, include it
on every log line for that request.

That covers *within* a single service. It does not, on its own, guarantee the header
is **forwarded on outbound calls** when one service calls another. Middleware that
reads an incoming header and logs it says nothing about whether the outbound
`HttpClient` call this service makes to another service *attaches* that same header.
If it doesn't, each service silently starts its own correlation ID instead of
propagating the caller's — the logs still look complete (every line has *a*
correlation ID), which is exactly what makes this easy to miss until you actually need
to trace one request across services and can't.

Containerizing doesn't cause this gap, but it's the first point in this project where
it can actually be tested for real, since Phase 1 never had a container network
boundary — outbound calls before Docker were `localhost` calls the same process could
still be reasoned about informally.

## Affected paths

Per project-context.md §6, there are exactly two inter-service HTTP calls in the
current architecture:

| Caller | Callee | Call |
|---|---|---|
| Payment | Subscription | `POST /api/v1/subscriptions/{id}/activate` (ADR 0003 bridge) |
| Streaming | Catalog | `GET /api/v1/titles/{titleId}` (diagnostic only, post-miss) |

Both are candidates for this gap. Both are also already flagged for other reasons in
`streaming-service-deferrals.md` (`§CatalogBoundary`) and project-context.md §10 (ADR
0003 bridge, removed in Phase 4) — this entry is specifically about the header, not
about the calls' broader deferral status. Don't conflate fixing this with removing the
ADR 0003 bridge; the bridge removal is scoped to Phase 4 regardless of this issue.

## Why it matters

- Without propagation, a single user-facing request that touches Payment →
  Subscription (or Streaming → Catalog) shows up as two or more *unrelated*
  correlation IDs in Serilog output. Debugging a failed payment activation means
  manually correlating by timestamp instead of by ID — exactly the failure mode
  structured logging with correlation IDs exists to prevent.
- This gets worse, not better, once RabbitMQ lands in Phase 4 — an async hop needs the
  correlation ID carried in the message envelope, which is a materially different
  mechanism than an HTTP header. Fixing the HTTP case now establishes the pattern
  (where does the ID live, how does a service read/attach it) before that harder
  version shows up.

## Verification steps (Phase 2, Week 3)

1. Issue a single request that triggers a cross-service call (e.g., the pay step,
   which causes Payment to call Subscription).
2. Capture the correlation ID from the Payment service's log line for the initiating
   request.
3. Grep the Subscription service's logs for that same correlation ID.
4. Repeat for Streaming → Catalog using a playback-position-miss scenario.

**Pass:** the same correlation ID appears in both services' logs for one logical
request.
**Fail:** Subscription (or Catalog) logs show a different, self-generated correlation
ID for the same request.

## Fix, if verification fails

Attach the correlation ID from `HttpContext.Items` onto the outbound `HttpClient`
request's `X-Correlation-Id` header in whatever client wrapper each service uses to
call the other (Payment's Subscription client, Streaming's `CatalogClient`). This is a
small, mechanical fix — the risk here is not fixing it, it's not noticing it's
missing, since everything still "looks" like it's logging correctly in isolation.

## Trigger criteria for closing this item

Verified per the steps above during Phase 2 Week 3, with the fix applied if the check
fails. Do not defer this past Phase 2 — Phase 7's correlation-ID tracing goal assumes
this already works and isn't scoped to re-verify it from scratch.
