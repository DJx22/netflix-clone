using Subscription.Application.DTOs;

namespace Subscription.Application.Services;

/// <summary>
/// Application service for plan read operations.
/// No authentication required to list plans — browsing before sign-up is an
/// explicit spec use case (<c>GET /api/v1/plans</c>).
/// </summary>
public interface IPlanService
{
    /// <summary>
    /// Returns all available subscription plans.
    /// </summary>
    Task<IReadOnlyList<PlanResponse>> ListPlansAsync(CancellationToken cancellationToken = default);
}
