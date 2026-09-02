using Subscription.Application.Abstractions;

namespace Subscription.Infrastructure.Services;

/// <summary>
/// Production implementation of <see cref="IDateTimeProvider"/> that delegates to
/// <see cref="DateTime.UtcNow"/>. Registered as <b>Singleton</b> because it holds
/// no mutable state — every call is a fresh clock read (§7).
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;
}
