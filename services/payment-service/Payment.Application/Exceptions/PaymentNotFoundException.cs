namespace Payment.Application.Exceptions;

/// <summary>
/// Thrown when a repository query returns no payment for the requested ID.
/// Maps to HTTP 404 at the API boundary.
/// </summary>
/// <remarks>
/// This is an Application-level concern, not a domain rule violation — the domain
/// models what a payment <em>is</em>; the application layer handles the case
/// where one does not exist for a given query.
/// </remarks>
public sealed class PaymentNotFoundException : Exception
{
    private PaymentNotFoundException(string message) : base(message) { }

    /// <summary>
    /// Creates an exception indicating no payment exists with the given ID.
    /// Used by <c>GET /api/v1/payments/{paymentId}</c>.
    /// </summary>
    public static PaymentNotFoundException ForPaymentId(Guid paymentId) =>
        new($"No payment found with ID '{paymentId}'.");
}
