namespace Subscription.Application.Abstractions;

/// <summary>
/// Provides the current UTC instant.
/// <para>
/// Abstracted so that <see cref="Services.SubscriptionService"/> can compute billing
/// periods without taking a static dependency on <see cref="DateTime.UtcNow"/>,
/// which makes the service unit-testable with a controlled clock.
/// </para>
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Gets the current UTC time.</summary>
    DateTime UtcNow { get; }
}
