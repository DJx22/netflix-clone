using Profile.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Profile.Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that starts a SQL Server container once per test collection,
/// applies EF Core migrations, and provides a clean <see cref="ProfileDbContext"/>
/// for each test. Satisfies §14: integration tests run against a real containerised
/// database, not a mock.
///
/// The container is started in <see cref="InitializeAsync"/> and stopped in
/// <see cref="DisposeAsync"/> — xUnit calls these automatically via IAsyncLifetime.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply schema via EnsureCreated — Profile has no migrations yet.
        // Once migrations are scaffolded, switch to context.Database.MigrateAsync().
        var options = BuildOptions(ConnectionString);
        await using var context = new ProfileDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new DbContext pointing at the container database.
    /// Each test should call this to get a fresh context (not shared state across tests).
    /// </summary>
    public ProfileDbContext CreateContext()
    {
        return new ProfileDbContext(BuildOptions(ConnectionString));
    }

    private static DbContextOptions<ProfileDbContext> BuildOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<ProfileDbContext>()
            .UseSqlServer(connectionString)
            .Options;
    }
}

/// <summary>
/// xUnit collection definition. All classes in [Collection(DatabaseCollection.Name)]
/// share the same container instance — starting SQL Server once for the collection
/// is much cheaper than once per class.
/// </summary>
[CollectionDefinition(DatabaseCollection.Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
