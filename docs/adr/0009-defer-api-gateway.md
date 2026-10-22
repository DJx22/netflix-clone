# ADR 0009: Defer the API Gateway

**Status:** Accepted  
**Date:** 2026-10-07  
**Deciders:** Dhruv (project owner)

## Context

The platform has six backend services, no frontend, and Postman as its only
client. The project has a tight 8GB RAM budget, and adding another running
service competes with the APIs and infrastructure needed for the current phase.

An API gateway is a standard pattern in production microservice systems. It can
provide a single client-facing entry point, route requests without exposing
service names or ports to clients, centralize client-facing authentication
rather than implementing it six times, and reduce the externally exposed
surface by exposing the gateway instead of each API.

Those are valid future benefits, but there is no current client or operational
problem that requires them. In particular, centralized authentication is not
being claimed as a fix for existing duplicated authentication. A gateway also
does not remove the need for appropriate authorization and protections between
services.

## Decision

Defer the API gateway for now. Phase 4 will add RabbitMQ and its
`PaymentCompleted → SubscriptionActivated` flow, but will not add YARP or a
gateway service. Postman will call the service APIs directly.

Track gateway evaluation and implementation in
[`docs/backlogs/api-gateway.md`](../backlogs/api-gateway.md). Revisit it when a
frontend or another client is introduced, direct service endpoints become an
operational burden, or the RAM budget has enough headroom to run it.

## Consequences

- No gateway process or routing configuration consumes memory in the current
  local stack.
- Postman requests remain coupled to individual service addresses and ports.
- Client-facing authentication and externally exposed endpoints are not
  consolidated at a gateway.
- The project defers, rather than rejects, a standard microservice pattern and
  retains a backlog for learning and adopting it when justified.
