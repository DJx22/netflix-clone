# API Gateway Backlog

Deferred by [ADR 0009](../adr/0009-defer-api-gateway.md) to keep the current
six-service, Postman-only stack within its RAM budget.

Revisit when a frontend or additional client is added, direct service routing
becomes burdensome, or local RAM headroom improves.

## Work to revisit

- [ ] Evaluate YARP as the gateway for the six service APIs.
- [ ] Define client-facing routes so clients do not depend on service names or
  ports.
- [ ] Decide which client authentication checks can be enforced once at the
  gateway; retain service-level authorization and internal protections where
  needed.
- [ ] Expose only the gateway to external clients and verify the resulting
  network boundary.
- [ ] Measure the full local stack's RAM use with the gateway before making it
  part of the default startup.
