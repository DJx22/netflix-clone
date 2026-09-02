using SubscriptionAggregate = Subscription.Domain.Aggregates.Subscription;
using Subscription.Application.DTOs;

namespace Subscription.Application.Mappings;

/// <summary>
/// Extension methods for mapping the <see cref="SubscriptionAggregate"/> aggregate
/// to response DTOs. Kept internal — callers outside Application never receive domain types.
/// </summary>
internal static class SubscriptionMappings
{
    /// <summary>Projects a <see cref="SubscriptionAggregate"/> to a <see cref="SubscriptionResponse"/>.</summary>
    internal static SubscriptionResponse ToResponse(this SubscriptionAggregate subscription) =>
        new(
            subscription.SubscriptionId,
            subscription.AccountId,
            subscription.PlanId,
            // ToString() produces the enum member name matching the spec strings exactly:
            // PendingPayment, Active, PastDue, Cancelled.
            subscription.Status.ToString(),
            subscription.CurrentPeriodEndUtc);
}
