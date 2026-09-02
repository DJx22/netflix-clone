namespace Payment.Application.Abstractions;

/// <summary>
/// Provides the current UTC instant.
/// </summary>
/// <remarks>
/// Abstracted so that <see cref="Services.PaymentService"/> can stamp
/// <c>ProcessedAtUtc</c> without taking a static dependency on <see cref="DateTime.UtcNow"/>,
/// which makes the service unit-testable with a controlled clock.
/// </remarks>
public interface IDateTimeProvider
{
    /// <summary>Gets the current UTC time.</summary>
    DateTime UtcNow { get; }
}
