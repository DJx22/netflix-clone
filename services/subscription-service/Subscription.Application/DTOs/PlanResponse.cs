namespace Subscription.Application.DTOs;

/// <summary>
/// Response DTO for a subscription plan.
/// Returned by <c>GET /api/v1/plans</c>. No auth required per spec.
/// </summary>
/// <param name="PlanId">String plan identifier (not a UUID — see OpenAPI spec).</param>
/// <param name="Name">Human-readable plan name.</param>
/// <param name="PriceMonthly">Monthly price in the default currency. Stored as decimal to avoid floating-point rounding; serialised as a JSON number.</param>
/// <param name="MaxProfiles">Maximum simultaneous viewer profiles permitted.</param>
/// <param name="VideoQuality">Quality tier label (e.g. "SD", "HD", "4K").</param>
/// <param name="Description">Optional marketing description; may be null.</param>
public sealed record PlanResponse(
    string PlanId,
    string Name,
    decimal PriceMonthly,
    int MaxProfiles,
    string VideoQuality,
    string? Description);
