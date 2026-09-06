using Catalog.Domain.Aggregates;
using Catalog.Infrastructure.Persistence;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Catalog.Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that starts a real MongoDB container once per test collection,
/// registers BSON class maps, applies indexes, and provides a fresh
/// <see cref="IMongoCollection{T}"/> for each test.
/// §14: Infrastructure gets integration tests against a real (containerised) database.
/// </summary>
/// <remarks>
/// The container is started in <see cref="InitializeAsync"/> and stopped in
/// <see cref="DisposeAsync"/> — xUnit calls these automatically via
/// <see cref="IAsyncLifetime"/>.
///
/// <b>Why a single container per collection?</b>
/// Starting a MongoDB container takes ~2–4 s.  Sharing it across all Infrastructure
/// tests in the collection is much cheaper than one container per class.  Each test
/// works with a distinct collection name (via <see cref="CreateCollection"/>) to
/// guarantee isolation without needing to clean up shared state.
/// </remarks>
public sealed class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:6.0").Build();

    private MongoClient _client = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Register BSON class maps once for the entire test session.
        // CatalogBsonConfiguration.Register() is idempotent — safe to call from here.
        CatalogBsonConfiguration.Register();

        _client = new MongoClient(_container.GetConnectionString());
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh, isolated <see cref="IMongoCollection{T}"/> for a single test.
    /// Each test uses a uniquely-named collection so tests never share state.
    /// </summary>
    public IMongoCollection<Title> CreateCollection()
    {
        // Unique collection name per call — xUnit may run tests in parallel within a collection
        // if [assembly: CollectionBehavior(DisableTestParallelization = false)].
        // A random suffix is the safest isolation strategy.
        var collectionName = $"titles_{Guid.NewGuid():N}";
        var db = _client.GetDatabase("CatalogTestDb");
        return db.GetCollection<Title>(collectionName);
    }
}

/// <summary>
/// xUnit collection definition.  All classes in [Collection(<see cref="Name"/>)] share
/// the same container instance — starting MongoDB once for the collection is much
/// cheaper than once per class.
/// </summary>
[CollectionDefinition(MongoDbCollection.Name)]
public sealed class MongoDbCollection : ICollectionFixture<MongoDbFixture>
{
    public const string Name = "MongoDb";
}
