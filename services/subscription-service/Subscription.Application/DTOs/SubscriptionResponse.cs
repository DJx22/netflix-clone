namespace Subscription.Application.DTOs;

/// <summary>
/// Response DTO for a subscription.
/// Returned by <c>GET /subscriptions/me</c>, <c>POST /subscriptions</c>,
/// and <c>PUT /subscriptions/me/plan</c>.
/// </summary>
/// <param name="SubscriptionId">Unique subscription identifier (UUID).</param>
/// <param name="AccountId">The account that owns this subscription (UUID).</param>
/// <param name="PlanId">The plan the subscription is currently on.</param>
/// <param name="Status">
/// Lifecycle status as a string matching the spec enum:
/// <c>PendingPayment</c>, <c>Active</c>, <c>PastDue</c>, <c>Cancelled</c>.
/// </param>
/// <param name="CurrentPeriodEndUtc">
/// UTC instant at which the current billing period ends.
/// Matches the <c>currentPeriodEndUtc</c> spec field; the UTC kind is enforced by
/// the domain's <c>BillingPeriod</c> value object, so this value is always UTC.
/// </param>
public sealed record SubscriptionResponse(
    Guid SubscriptionId,
    Guid AccountId,
    string PlanId,
    string Status,
    DateTime CurrentPeriodEndUtc);
