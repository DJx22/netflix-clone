using Microsoft.EntityFrameworkCore;
using Subscription.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Subscription.Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that starts a SQL Server container once per test collection,
/// applies EF Core migrations, and provides a fresh <see cref="SubscriptionDbContext"/>
/// for each test. Satisfies §14: "Infrastructure gets integration tests against a real
/// (containerised) database, not mocks."
/// <para>
/// The container is started in <see cref="InitializeAsync"/> and stopped in
/// <see cref="DisposeAsync"/> — xUnit calls these automatically via IAsyncLifetime.
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>Full connection string to the running container database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply all pending migrations so the schema is ready before any test runs.
        // MigrateAsync proves the real migration path works (not just EF schema inference).
        var options = BuildOptions(ConnectionString);
        await using var context = new SubscriptionDbContext(options);
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh <see cref="SubscriptionDbContext"/> for a single test.
    /// Each test should call this to avoid shared change-tracking state.
    /// </summary>
    public SubscriptionDbContext CreateContext()
    {
        return new SubscriptionDbContext(BuildOptions(ConnectionString));
    }

    private static DbContextOptions<SubscriptionDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<SubscriptionDbContext>()
            .UseSqlServer(connectionString)
            .Options;
}

/// <summary>
/// xUnit collection definition. All classes in [Collection(<see cref="Name"/>)] share
/// the same container instance — starting SQL Server once for the collection is much
/// cheaper than once per class.
/// </summary>
[CollectionDefinition(DatabaseCollection.Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
