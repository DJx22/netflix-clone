namespace Subscription.Application.DTOs;

/// <summary>
/// Request DTO for <c>POST /api/v1/subscriptions</c>.
/// Validated by <see cref="Validators.CreateSubscriptionRequestValidator"/>
/// before it reaches application logic.
/// </summary>
/// <param name="PlanId">The plan to subscribe to. Must be non-empty; existence is verified by the service.</param>
public sealed record CreateSubscriptionRequest(string PlanId);
