namespace Subscription.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the <c>ConnectionStrings</c> configuration section.
/// Resolved via <c>IOptions&lt;ConnectionStringOptions&gt;</c> — never read through
/// <c>IConfiguration["key"]</c> directly (§6).
/// </summary>
public sealed class ConnectionStringOptions
{
    /// <summary>Gets the ADO.NET connection string for the Subscription service's SQL Server database.</summary>
    public string SubscriptionDb { get; init; } = string.Empty;
}
