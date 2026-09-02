using Subscription.Application.DTOs;
using Subscription.Application.Mappings;
using Subscription.Domain.Interfaces;

namespace Subscription.Application.Services;

/// <summary>
/// Application service that fulfils <c>GET /api/v1/plans</c>.
/// Thin orchestration: retrieve from repository, project to DTO, return.
/// No mutation — this service only reads.
/// </summary>
public sealed class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepository;

    /// <summary>Initialises a new <see cref="PlanService"/>.</summary>
    public PlanService(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlanResponse>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository
            .ListAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return plans
            .Select(p => p.ToResponse())
            .ToList()
            .AsReadOnly();
    }
}
