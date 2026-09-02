# ADR 0003: Temporary Direct Call for Subscription Activation (Phases 1–3)

## Status
Accepted

## Context
`ActivateSubscriptionAsync` already exists in the Subscription service's Application layer. The intended long-term design (roadmap §4, §7 — Phase 4) is for it to be invoked by a RabbitMQ consumer reacting to Payment's `PaymentCompleted` event, in-process, with zero HTTP call between the two services.

RabbitMQ isn't running until Phase 2 brings up containers, and isn't wired for messaging until Phase 4 specifically. But Phase 1's own "done when" checkpoint (roadmap §7) requires the full register → profile → plan → pay → browse flow to work end-to-end over Swagger, with no containers at all. Something has to trigger activation now, without a broker.

## Decision
Add `POST /api/v1/subscriptions/{subscriptionId}/activate` to the Subscription service. In Phases 1–3, Payment calls this endpoint directly and synchronously immediately after recording a successful mock charge. In Phase 4, this direct call is replaced — not by deleting the endpoint, but by no longer using it as the trigger — with a RabbitMQ consumer inside Subscription that invokes the same `ActivateSubscriptionAsync` method in-process.

## Consequences

**Positive**
- Phase 1's checkpoint stays honestly testable end-to-end via Swagger before any messaging infrastructure exists.
- Phase 4's "zero direct HTTP call" checkpoint becomes a real, legible before/after transformation instead of a feature addition with nothing to contrast it against. The reason that checkpoint is worth writing down at all is that there's something concrete to remove.

**Negative**
- Phases 1–3 temporarily reintroduce exactly the tight coupling and cascading-failure risk — if Subscription is down, Payment's activation call fails synchronously — that the Phase 4 architecture exists to remove. Not a hidden cost; recorded here so it isn't mistaken for the target design.
- No service-to-service authentication exists yet. The endpoint currently trusts anything that can reach it on the local network. Acceptable for mock data in a learning project; would need real service auth (mTLS, an internal API key, or similar) before this pattern touched anything real.

**Revisit trigger**
- Phase 4: build the RabbitMQ consumer, point it at the same Application-layer method, and stop calling this endpoint from Payment. Decide then whether to leave the endpoint as a manual/admin escape hatch or lock it down — not before Phase 4 exists.
