namespace Streaming.Application.Abstractions;

/// <summary>
/// Provides the current UTC instant.
/// Abstracted so that <see cref="Services.PlaybackService"/> can stamp
/// <c>UpdatedAtUtc</c> without a static dependency on <see cref="DateTime.UtcNow"/>,
/// keeping the service fully unit-testable with a controlled clock.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Gets the current UTC time.</summary>
    DateTime UtcNow { get; }
}
