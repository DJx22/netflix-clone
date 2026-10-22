# Backlog: Correlation ID Propagation Across Container Network Boundaries

**Status:** Open — container-to-container correlation ID propagation has not yet
been verified.

**§ tag:** `§CorrelationIdContainerPropagation`

**Trigger:** Verify during container integration before relying on end-to-end
correlation ID tracing in observability work.

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

Containerizing doesn't cause this gap, but it provides a real service-network
boundary where propagation can be verified.

## Affected paths

The current architecture has one inter-service HTTP call:

| Caller | Callee | Call |
|---|---|---|
| Streaming | Catalog | `GET /api/v1/titles/{titleId}` (diagnostic only, post-miss) |

Payment-to-Subscription activation uses RabbitMQ rather than HTTP; correlation ID
propagation for that asynchronous hop is carried in the message header and is outside
the scope of this entry. This entry tracks only HTTP header propagation.

## Why it matters

- Without propagation, a request that touches Streaming → Catalog can appear under
  unrelated correlation IDs in the two services' logs, making cross-service
  troubleshooting harder.
- Asynchronous messaging carries correlation IDs in message headers; that mechanism
  is separate from forwarding an HTTP header.

## Verification

1. Trigger a playback-position miss that causes Streaming to call Catalog.
2. Capture the correlation ID from the Streaming service's log.
3. Check whether the same ID appears in the Catalog service's logs.

**Pass:** the same correlation ID appears in both services' logs for the request.
**Fail:** Catalog logs a different, self-generated correlation ID.

## Fix, if verification fails

If verification fails, attach the correlation ID from `HttpContext.Items` to the
outbound `HttpClient` request's `X-Correlation-Id` header in Streaming's
`CatalogClient`.

## Closing criteria

Close this item when the verification passes, applying the fix above if needed.
