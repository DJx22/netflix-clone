namespace Payment.Application.DTOs;

/// <summary>
/// Response DTO for all payment endpoints.
/// Returned by <c>POST /api/v1/payments</c>, <c>GET /api/v1/payments</c>,
/// and <c>GET /api/v1/payments/{paymentId}</c>.
/// </summary>
/// <remarks>
/// Status is serialised as a string matching the spec enum exactly:
/// <c>Succeeded</c> or <c>Failed</c>. <c>Pending</c> is a domain-internal state
/// and must never appear in a response — the service always resolves the charge
/// before returning (openapi.yaml: "Mock charge processed — status Succeeded or Failed either way").
/// </remarks>
/// <param name="PaymentId">Unique payment identifier (UUID).</param>
/// <param name="SubscriptionId">The subscription this payment billed.</param>
/// <param name="Amount">The charged amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Status">Charge outcome: <c>Succeeded</c> or <c>Failed</c>.</param>
/// <param name="ProcessedAtUtc">UTC instant at which the charge was processed.</param>
public sealed record PaymentResponse(
    Guid PaymentId,
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime ProcessedAtUtc);
