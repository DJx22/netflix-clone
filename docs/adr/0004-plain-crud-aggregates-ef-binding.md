# ADR 0004: Plain-CRUD Aggregates Use Native EF Core Binding, Not Reconstitute Projection

## Status
Accepted

## Context
The original design for the Subscription aggregate used a private constructor plus a static `Reconstitute` factory, with repositories projecting EF query results to an intermediate row shape before calling `Reconstitute`, and writes going through `AddAsync`/`UpdateAsync` with manually attached entities.

Two problems with that, not one:

1. **The premise behind it was wrong.** EF Core can call a private constructor directly, including one with parameters, as long as the parameter names/types match the mapped properties by convention (or are configured explicitly via `HasConstructor`). This has been supported since EF Core 3.0. "Relax constructor visibility" was never the actual alternative to `Reconstitute` — a correctly-shaped private constructor gets native binding for free.
2. **The pattern itself was heavier than the service's scope.** Phase 1 (roadmap §8) already decided *"Catalog gets Mediator/CQRS; everything else stays plain CRUD."* Private constructor + factory + a parallel persistence-row model is real DDD tactical machinery, appropriate for an aggregate with invariants complex enough to need that much protection. Subscription is a 4-value status enum with a linear transition path — that doesn't clear the bar the pattern is for.

## Decision
Plain-CRUD aggregates (Subscription, and by the same reasoning Identity, Profile, Payment — everything except Catalog) use a single constructor whose parameter names match the mapped columns, private setters for everything else, and behavior methods as the only way to transition state after construction:

```csharp
public sealed class Subscription
{
    public Guid SubscriptionId { get; private set; }
    public Guid AccountId { get; private set; }
    public string PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime CurrentPeriodEndUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    public Subscription(Guid subscriptionId, Guid accountId, string planId, DateTime createdAtUtc)
    {
        // validate, assign, Status = PendingPayment, seed CurrentPeriodEndUtc
    }

    public void Activate(Guid paymentId) { /* idempotent; no-op if already Active; typed failure if Cancelled */ }
    public void Cancel() { /* sets Cancelled; CurrentPeriodEndUtc untouched — takes effect there, not immediately */ }
    public void ChangePlan(string newPlanId) { /* PlanId reassignment + proration rule */ }
}
```

Rules that make this actually work, not just look like it works:

- **One constructor only.** No second parameterless private constructor "just in case" — that reintroduces the exact ambiguity this is meant to avoid.
- **Constructor parameter names must match mapped property names**, case-insensitively, for EF's convention-based binding to resolve with zero extra configuration. This is a naming discipline, not a one-time setup step — a property rename without a matching constructor-parameter rename breaks binding silently at model-build time, not as an obvious compile error.
- **Everything not in the constructor** (`Status`, `CurrentPeriodEndUtc`, `RowVersion`) needs a private setter — EF sets those via reflection in the same materialization pass, no second step.
- **`RowVersion` gets one line of Infrastructure config**: `.Property(x => x.RowVersion).IsRowVersion()`. Nothing else needs configuring — no value converters, no owned-type mapping, because nothing here requires them.

Repositories query the `DbSet` directly and get back tracked entities. Writes call a behavior method and `SaveChangesAsync` — no manual `Attach`, no manual entity-state setting.

## Consequences

**Positive**
- Less code — no parallel persistence-row type per aggregate, no explicit projection step in every repository method.
- Correct EF change tracking: `SaveChangesAsync` diffs and writes only what actually changed, instead of the manually-attached path marking every property Modified and touching every column on every write.
- Real optimistic concurrency via `RowVersion`, which the manually-attached path didn't have unless configured separately anyway.
- Matches the scope already decided for every service except Catalog.

**Negative**
- Relies on constructor-parameter-to-property name matching as an ongoing discipline, not a one-time decision. Get sloppy with a rename later and the failure mode is a runtime model-build error, not a compiler error.
- Doesn't extend cleanly to an aggregate with value objects needing custom conversion, constructor-bound collections, or invariants spanning enough properties that a single constructor can't validate them cleanly. That's a real limit, not a hypothetical one.

**Revisit trigger**
- If any aggregate — Subscription or otherwise — grows real invariant complexity (child entities, value objects, cross-property invariants a constructor can't validate alone), reconsider the private-constructor + `Reconstitute` + projection pattern **for that aggregate specifically**. This ADR settles the plain-CRUD services; it isn't a project-wide ban on the heavier pattern where something actually earns it. Catalog's CQRS path is already a separate concern and isn't addressed here.
