namespace Payment.Domain.Enums;

/// <summary>
/// The lifecycle status of a payment charge attempt.
/// </summary>
/// <remarks>
/// <c>Pending</c> is a domain-internal transient state that exists only in memory
/// between construction and outcome recording. It must never be persisted; the
/// Application layer enforces this by always calling <c>RecordSuccess</c> or
/// <c>RecordFailure</c> before <c>AddAsync</c>.
/// The spec's API response only exposes <c>Succeeded</c> and <c>Failed</c>
/// (openapi.yaml PaymentResponse.status) because the mock charge resolves
/// synchronously within the same request.
/// </remarks>
public enum PaymentStatus
{
    /// <summary>
    /// Initial transient state — charge attempt has been created but not yet resolved.
    /// Domain-internal only; must not be persisted or returned via API.
    /// </summary>
    Pending,

    /// <summary>The charge was accepted by the mock payment processor.</summary>
    Succeeded,

    /// <summary>The charge was declined by the mock payment processor.</summary>
    Failed
}

