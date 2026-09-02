using PaymentAggregate = Payment.Domain.Aggregates.Payment;
using Payment.Application.DTOs;

namespace Payment.Application.Mappings;

/// <summary>
/// Extension methods for mapping the <see cref="PaymentAggregate"/> aggregate
/// to response DTOs. Kept internal — callers outside Application never receive domain types.
/// </summary>
internal static class PaymentMappings
{
    /// <summary>
    /// Projects a <see cref="PaymentAggregate"/> to a <see cref="PaymentResponse"/>.
    /// </summary>
    internal static PaymentResponse ToResponse(this PaymentAggregate payment) =>
        new(
            payment.PaymentId,
            payment.SubscriptionId,
            payment.ChargedAmount.Amount,
            payment.ChargedAmount.Currency,
            // ToString() produces the enum member name matching spec strings exactly: Succeeded, Failed.
            // Pending must never reach this mapping — the service resolves the charge before persisting.
            payment.Status.ToString(),
            payment.ProcessedAtUtc);
}
