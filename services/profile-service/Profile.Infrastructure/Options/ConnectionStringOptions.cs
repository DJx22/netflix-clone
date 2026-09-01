namespace Profile.Infrastructure.Options;

/// <summary>
/// Strongly-typed binding for the "ConnectionStrings" configuration section.
/// The value comes from environment variables / secret store at runtime — never from source (§12).
/// </summary>
public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    /// <summary>
    /// Connection string for ProfileDb on the shared SQL Server container (ADR 0001).
    /// Must point only at ProfileDb — no cross-service connection strings here.
    /// </summary>
    public string ProfileDb { get; init; } = string.Empty;
}
