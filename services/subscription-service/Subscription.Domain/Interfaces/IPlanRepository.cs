using Subscription.Domain.Entities;

namespace Subscription.Domain.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Plan"/> reference entities.
/// Implemented in <c>Subscription.Infrastructure</c>; consumed by <c>Subscription.Application</c>.
/// </summary>
public interface IPlanRepository
{
    /// <summary>
    /// Returns the plan with the given identifier, or <see langword="null"/> if it does not exist.
    /// </summary>
    /// <param name="planId">The string plan identifier (not a GUID — see OpenAPI spec).</param>
    Task<Plan?> FindByIdAsync(string planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all available subscription plans.
    /// </summary>
    /// <remarks>
    /// Used by the <c>GET /api/v1/plans</c> endpoint which requires no authentication —
    /// browsing plans before sign-up is an explicit spec use case.
    /// </remarks>
    Task<IReadOnlyList<Plan>> ListAllAsync(CancellationToken cancellationToken = default);
}
