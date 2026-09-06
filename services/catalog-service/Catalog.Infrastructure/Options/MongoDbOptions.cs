namespace Catalog.Infrastructure.Options;

/// <summary>
/// Strongly-typed configuration for the Catalog service's MongoDB connection.
/// Bound from <c>appsettings.json</c> section <c>"MongoDb"</c> via IOptions&lt;T&gt; (§6).
/// </summary>
/// <remarks>
/// IConfiguration["key"] access outside Program.cs is explicitly forbidden by §6.
/// All MongoDB configuration therefore flows through this class — nothing in
/// Infrastructure reads the configuration dictionary directly at runtime.
/// </remarks>
public sealed class MongoDbOptions
{
    /// <summary>Configuration section key used when binding from appsettings.</summary>
    public const string SectionName = "MongoDb";

    /// <summary>
    /// Full MongoDB connection string, e.g.
    /// <c>mongodb://localhost:27017</c> or an Atlas SRV URI.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the database that hosts the <c>titles</c> collection.
    /// Defaults to <c>"CatalogDb"</c> so local dev works without extra config.
    /// </summary>
    public string DatabaseName { get; set; } = "CatalogDb";

    /// <summary>
    /// Name of the collection that stores <c>Title</c> documents.
    /// Separated from <see cref="DatabaseName"/> so integration tests can
    /// point at a different collection without a different database.
    /// </summary>
    public string TitlesCollectionName { get; set; } = "titles";
}
