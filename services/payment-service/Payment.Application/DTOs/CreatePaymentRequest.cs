namespace Payment.Application.DTOs;

/// <summary>
/// Request DTO for <c>POST /api/v1/payments</c>.
/// Maps directly from the spec's <c>CreatePaymentRequest</c> schema (openapi.yaml).
/// </summary>
/// <param name="SubscriptionId">The subscription being billed. Must be a non-empty GUID.</param>
/// <param name="Amount">
/// The charge amount. Must be zero or greater (free-tier / trial allowed).
/// Received as <c>float</c> from the wire; converted to <c>decimal</c> here at the boundary
/// so monetary arithmetic in the domain uses exact representation.
/// </param>
/// <param name="Currency">
/// ISO 4217 three-letter currency code (e.g. "USD", "INR").
/// Structural validation (length == 3) is enforced in the validator.
/// </param>
/// <param name="SimulateFailure">
/// Testing-only field. <see langword="null"/> means randomised outcome (spec default).
/// <see langword="true"/> forces a failed charge. Never persisted on the aggregate.
/// </param>
public sealed record CreatePaymentRequest(
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    bool? SimulateFailure);
