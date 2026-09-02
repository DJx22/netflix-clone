using Subscription.Domain.Enums;
using Subscription.Domain.Exceptions;

namespace Subscription.Domain.Aggregates;

/// <summary>
/// The Subscription aggregate root.
/// <para>
/// Lifecycle: <c>PendingPayment</c> → <c>Active</c> (on PaymentCompleted) →
/// <c>Cancelled</c> or <c>PastDue</c>. See <c>openapi.yaml</c> for the full status
/// enum and the event-driven activation model (ADR 0003, ADR 0004).
/// </para>
/// </summary>
/// <remarks>
/// ADR 0004: this is a plain-CRUD aggregate. EF Core binds this single constructor
/// by matching parameter names to mapped property names, case-insensitively.
/// One constructor only — a second constructor reintroduces the ambiguity this pattern
/// removes. Rename a property? Rename the matching constructor parameter too, or EF
/// fails at model-build time, not compile time.
/// </remarks>
public sealed class Subscription
{
    /// <summary>Gets the unique subscription identifier (UUID per ADR 0001).</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Gets the account that owns this subscription.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Gets the plan the subscription is currently on.</summary>
    public string PlanId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>
    /// Gets the UTC instant at which the current billing period ends.
    /// Cancellation is recorded immediately but access continues until this instant.
    /// </summary>
    public DateTime CurrentPeriodEndUtc { get; private set; }

    /// <summary>Gets the UTC instant the subscription was created. Sort key for "most recent" queries.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Optimistic-concurrency token. Set by EF Core; never touched by domain logic.</summary>
    public byte[] RowVersion { get; private set; } = default!;

    /// <summary>Returns <see langword="true"/> when the subscription is in <c>Active</c> status.</summary>
    public bool IsActive => Status == SubscriptionStatus.Active;

    /// <summary>Returns <see langword="true"/> when the subscription is awaiting payment confirmation.</summary>
    public bool IsPendingPayment => Status == SubscriptionStatus.PendingPayment;

    /// <summary>Returns <see langword="true"/> when the subscription has been cancelled.</summary>
    public bool IsCancelled => Status == SubscriptionStatus.Cancelled;

    /// <summary>Returns <see langword="true"/> when the subscription is past due.</summary>
    public bool IsPastDue => Status == SubscriptionStatus.PastDue;

    /// <summary>
    /// Creates a new subscription in <c>PendingPayment</c> status, and seeds
    /// <see cref="CurrentPeriodEndUtc"/> to <paramref name="createdAtUtc"/> + 1 month.
    /// </summary>
    /// <remarks>
    /// EF Core uses this same constructor to materialise persisted rows by matching
    /// parameter names to property names. Properties not listed here (<c>Status</c>,
    /// <c>CurrentPeriodEndUtc</c>, <c>RowVersion</c>) are written by EF via their
    /// private setters during materialisation, overwriting the values set below.
    /// </remarks>
    /// <param name="subscriptionId">A fresh, non-empty GUID supplied by the caller.</param>
    /// <param name="accountId">The owning account; must be non-empty.</param>
    /// <param name="planId">The plan being subscribed to; must be non-empty.</param>
    /// <param name="createdAtUtc">
    /// The creation instant (UTC). Application layer supplies this from
    /// <c>IDateTimeProvider.UtcNow</c> — the domain does not call <c>DateTime.UtcNow</c> directly.
    /// </param>
    public Subscription(
        Guid subscriptionId,
        Guid accountId,
        string planId,
        DateTime createdAtUtc)
    {
        if (subscriptionId == Guid.Empty)
        {
            throw new ArgumentException("Subscription ID must not be empty.", nameof(subscriptionId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID must not be empty.", nameof(accountId));
        }

        if (string.IsNullOrWhiteSpace(planId))
        {
            throw new ArgumentException("Plan ID must not be empty.", nameof(planId));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Created-at timestamp must be UTC.", nameof(createdAtUtc));
        }

        SubscriptionId      = subscriptionId;
        AccountId           = accountId;
        PlanId              = planId;
        CreatedAtUtc        = createdAtUtc;
        // Monthly billing cadence inferred from the spec's priceMonthly field.
        // If a non-monthly cycle is later confirmed, this is the single place to update.
        CurrentPeriodEndUtc = createdAtUtc.AddMonths(1);
        Status              = SubscriptionStatus.PendingPayment;
    }

    /// <summary>
    /// Transitions the subscription to <c>Active</c> status.
    /// </summary>
    /// <param name="paymentId">
    /// The payment that triggered activation. Optional for Phase 1–3 HTTP-bridge
    /// compatibility (ADR 0003) where no paymentId is available at the call site.
    /// Phase 4's RabbitMQ consumer will supply a real value.
    /// </param>
    /// <remarks>
    /// Idempotent per ADR 0004: if already <c>Active</c>, returns without error so
    /// a duplicate PaymentCompleted delivery does not fault the consumer.
    /// </remarks>
    /// <exception cref="InvalidSubscriptionOperationException">
    /// Thrown if the subscription is <c>Cancelled</c> (terminal) or in any
    /// status other than <c>PendingPayment</c> or <c>Active</c>.
    /// </exception>
    public void Activate(Guid paymentId = default)
    {
        if (Status == SubscriptionStatus.Active)
        {
            return; // duplicate delivery — safe to ignore
        }

        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionOperationException(
                "Cannot activate a cancelled subscription. Cancelled is a terminal state.");
        }

        if (Status != SubscriptionStatus.PendingPayment)
        {
            throw new InvalidSubscriptionOperationException(
                $"Cannot activate a subscription in '{Status}' status. " +
                "Only PendingPayment subscriptions can be activated.");
        }

        Status = SubscriptionStatus.Active;
    }

    /// <summary>
    /// Cancels the subscription.
    /// </summary>
    /// <remarks>
    /// The spec says "takes effect at <c>currentPeriodEndUtc</c>, not immediately" —
    /// the status is written to <c>Cancelled</c> right now, but content access continues
    /// until <see cref="CurrentPeriodEndUtc"/>. The consuming service enforces that cutoff.
    /// </remarks>
    /// <exception cref="InvalidSubscriptionOperationException">
    /// Thrown if the subscription is not in <c>Active</c> or <c>PastDue</c> status.
    /// </exception>
    public void Cancel()
    {
        if (Status != SubscriptionStatus.Active && Status != SubscriptionStatus.PastDue)
        {
            throw new InvalidSubscriptionOperationException(
                $"Cannot cancel a subscription in '{Status}' status. " +
                "Only Active or PastDue subscriptions can be cancelled.");
        }

        Status = SubscriptionStatus.Cancelled;
    }

    /// <summary>Changes the plan the subscription is on.</summary>
    /// <param name="newPlanId">The new plan identifier; must be non-empty.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="newPlanId"/> is null or whitespace.</exception>
    /// <exception cref="InvalidSubscriptionOperationException">
    /// Thrown when the current status makes a plan change meaningless or impossible.
    /// </exception>
    public void ChangePlan(string newPlanId)
    {
        if (string.IsNullOrWhiteSpace(newPlanId))
        {
            throw new ArgumentException("New plan ID must not be empty.", nameof(newPlanId));
        }

        // Changing a plan before payment is meaningless — the subscription isn't confirmed yet.
        if (Status == SubscriptionStatus.PendingPayment)
        {
            throw new InvalidSubscriptionOperationException(
                "Cannot change the plan while a payment is still pending.");
        }

        // Cancelled is terminal — a new subscription must be created.
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionOperationException(
                "Cannot change the plan of a cancelled subscription.");
        }

        PlanId = newPlanId;
    }

    /// <summary>Marks the subscription as <c>PastDue</c> when a renewal payment fails.</summary>
    /// <exception cref="InvalidSubscriptionOperationException">
    /// Thrown if the subscription is not currently <c>Active</c>.
    /// </exception>
    public void MarkPastDue()
    {
        // PastDue is reached via a scheduled billing job, not through any API endpoint.
        if (Status != SubscriptionStatus.Active)
        {
            throw new InvalidSubscriptionOperationException(
                $"Cannot mark a subscription as PastDue from '{Status}' status. " +
                "Only Active subscriptions can become PastDue.");
        }

        Status = SubscriptionStatus.PastDue;
    }

    /// <summary>Advances the billing period to the next cycle on successful renewal.</summary>
    /// <param name="newPeriodEndUtc">The new period end; must be a UTC <see cref="DateTime"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="newPeriodEndUtc"/> is not UTC.</exception>
    /// <exception cref="InvalidSubscriptionOperationException">
    /// Thrown if the subscription is not currently <c>Active</c>.
    /// </exception>
    public void RenewPeriod(DateTime newPeriodEndUtc)
    {
        if (newPeriodEndUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("New period end must be a UTC DateTime.", nameof(newPeriodEndUtc));
        }

        if (Status != SubscriptionStatus.Active)
        {
            throw new InvalidSubscriptionOperationException(
                $"Cannot renew the billing period of a subscription in '{Status}' status.");
        }

        CurrentPeriodEndUtc = newPeriodEndUtc;
    }
}
