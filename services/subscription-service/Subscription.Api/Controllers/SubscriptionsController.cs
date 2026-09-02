using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscription.Application.DTOs;
using Subscription.Application.Services;

namespace Subscription.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for subscription lifecycle use cases.
/// All business logic lives in <see cref="ISubscriptionService"/>; this class only
/// maps HTTP to use-case calls and results back to HTTP (§11).
/// </summary>
[ApiController]
[Route(Routes.SubscriptionsBase)]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    /// <summary>Initialises a new <see cref="SubscriptionsController"/>.</summary>
    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/subscriptions/me
    // -------------------------------------------------------------------------

    /// <summary>Return the caller's current subscription in whatever status it's in.</summary>
    /// <response code="200">Current subscription.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No subscription exists yet for this account.</response>
    [HttpGet(Routes.Me)]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _subscriptionService.GetSubscriptionByAccountIdAsync(
            accountId.Value, cancellationToken);

        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  DELETE /api/v1/subscriptions/me
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cancel the caller's subscription.
    /// The cancellation is recorded immediately; access continues until
    /// <c>currentPeriodEndUtc</c>, not immediately (openapi.yaml).
    /// </summary>
    /// <response code="204">Cancelled.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No subscription exists for this account.</response>
    [HttpDelete(Routes.Me)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMySubscription(CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        await _subscriptionService.CancelSubscriptionAsync(accountId.Value, cancellationToken);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  PUT /api/v1/subscriptions/me/plan
    // -------------------------------------------------------------------------

    /// <summary>Change the plan on the caller's current subscription.</summary>
    /// <response code="200">Updated subscription.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No subscription exists for this account.</response>
    [HttpPut(Routes.MePlan)]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeMyPlan(
        [FromBody] ChangePlanRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _subscriptionService.ChangePlanAsync(
            accountId.Value, request, cancellationToken);

        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  POST /api/v1/subscriptions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Start a new subscription in <c>PendingPayment</c> status.
    /// Returns 202 (not 201) because the subscription is not yet Active —
    /// activation happens asynchronously when a PaymentCompleted event is
    /// consumed from RabbitMQ (openapi.yaml, Phase 4; ADR 0003 for Phases 1–3).
    /// </summary>
    /// <response code="202">Subscription created, pending payment.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="409">Account already has an active or pending subscription.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _subscriptionService.CreateSubscriptionAsync(
            accountId.Value, request, cancellationToken);

        // 202 Accepted — subscription exists but is not yet Active.
        return Accepted(result);
    }

    // -------------------------------------------------------------------------
    //  POST /api/v1/subscriptions/{subscriptionId}/activate   [ADR 0003 bridge]
    // -------------------------------------------------------------------------

    /// <summary>
    /// ADR 0003 Phase 1–3 bridge: Payment service calls this endpoint synchronously
    /// to activate a subscription after a successful payment, until the RabbitMQ
    /// consumer (Phase 4) replaces it. This endpoint is intentionally unauthenticated
    /// in Phase 1–3 — ADR 0003 records this as a known, accepted risk.
    /// </summary>
    /// <param name="subscriptionId">The subscription to activate.</param>
    /// <response code="204">Activated (or already active — idempotent per ADR 0004).</response>
    /// <response code="404">No subscription with that ID exists.</response>
    [HttpPost(Routes.ActivateById)]
    [AllowAnonymous]  // Overrides [Authorize] on the controller — ADR 0003 conscious decision.
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateSubscription(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        await _subscriptionService.ActivateSubscriptionAsync(subscriptionId, cancellationToken);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts the account ID from the JWT <c>sub</c> claim.
    /// Returns <see langword="null"/> if the claim is missing or not a valid <see cref="Guid"/>.
    /// Three-fallback chain matches Identity's token-issuance and Profile's claim-reading
    /// patterns so all services resolve the account ID consistently.
    /// </summary>
    private Guid? GetAccountId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid");

        return Guid.TryParse(sub, out var accountId) ? accountId : null;
    }
}
