using Subscription.Application.DTOs;
using Subscription.Domain.Entities;

namespace Subscription.Application.Mappings;

/// <summary>
/// Extension methods for mapping <see cref="Plan"/> domain entities to response DTOs.
/// Kept internal — callers outside Application should never receive domain types.
/// </summary>
internal static class PlanMappings
{
    /// <summary>Projects a <see cref="Plan"/> to a <see cref="PlanResponse"/>.</summary>
    internal static PlanResponse ToResponse(this Plan plan) =>
        new(
            plan.PlanId,
            plan.Name,
            plan.PriceMonthly.Amount,
            plan.MaxProfiles,
            plan.VideoQuality.Value,
            plan.Description);
}
