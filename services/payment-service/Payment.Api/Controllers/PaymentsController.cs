using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Application.Services;

namespace Payment.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for payment processing use cases.
/// All business logic lives in <see cref="IPaymentService"/>; this class only
/// maps HTTP to use-case calls and results back to HTTP (§11).
/// </summary>
[ApiController]
[Route(Routes.PaymentsBase)]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    /// <summary>Initialises a new <see cref="PaymentsController"/>.</summary>
    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/payments
    // -------------------------------------------------------------------------

    /// <summary>List the caller's payment history for their subscription.</summary>
    /// <response code="200">Payment history (may be empty if no payments exist).</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPayments(
        [FromHeader(Name = "X-Subscription-Id")] Guid? subscriptionId,
        CancellationToken cancellationToken)
    {
        // The spec's GET /payments says "List the caller's payment history" but the
        // request schema has no subscriptionId query/path param — only the POST body does.
        // The JWT carries accountId, not subscriptionId. Since Payment only stores
        // payments by subscriptionId (not accountId), the controller needs a subscriptionId
        // to query. This is an open question (see notes below); for now, accept it as an
        // optional header. If omitted, return an empty list rather than 400 — the caller
        // may not have a subscription yet.
        if (subscriptionId is null || subscriptionId == Guid.Empty)
        {
            return Ok(Array.Empty<PaymentResponse>());
        }

        var result = await _paymentService.GetPaymentsBySubscriptionIdAsync(
            subscriptionId.Value, cancellationToken);

        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  GET /api/v1/payments/{paymentId}
    // -------------------------------------------------------------------------

    /// <summary>Get a single payment by its ID.</summary>
    /// <param name="paymentId">The payment to retrieve.</param>
    /// <response code="200">Payment detail.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="404">No payment with that ID.</response>
    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId, cancellationToken);
        return Ok(result);
    }

    // -------------------------------------------------------------------------
    //  POST /api/v1/payments
    // -------------------------------------------------------------------------

    /// <summary>
    /// Submit a mock payment charge.
    /// Returns 201 with the completed charge — status is Succeeded or Failed.
    /// On success, publishes PaymentCompleted so the Subscription service can
    /// activate the subscription (Phase 4 via RabbitMQ; Phase 1–3 via ADR 0003 HTTP bridge).
    /// </summary>
    /// <response code="201">Charge processed — Succeeded or Failed.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.CreatePaymentAsync(request, cancellationToken);

        // 201 Created with Location header pointing to the new resource.
        return CreatedAtAction(
            nameof(GetPayment),
            new { paymentId = result.PaymentId },
            result);
    }

    // -------------------------------------------------------------------------
    //  Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts the account ID from the JWT <c>sub</c> claim.
    /// Returns <see langword="null"/> if the claim is missing or not a valid <see cref="Guid"/>.
    /// Three-fallback chain matches Identity's token-issuance and Subscription's claim-reading
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
