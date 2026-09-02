namespace Subscription.Application.DTOs;

/// <summary>
/// Request DTO for <c>PUT /api/v1/subscriptions/me/plan</c>.
/// Validated by <see cref="Validators.ChangePlanRequestValidator"/>
/// before it reaches application logic.
/// </summary>
/// <param name="NewPlanId">The plan to switch to. Must be non-empty; existence is verified by the service.</param>
public sealed record ChangePlanRequest(string NewPlanId);
