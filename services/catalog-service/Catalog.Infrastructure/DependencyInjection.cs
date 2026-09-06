using Catalog.Application.Repositories;
using Catalog.Domain.Aggregates;
using Catalog.Infrastructure.Indexes;
using Catalog.Infrastructure.Options;
using Catalog.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Catalog.Infrastructure;

/// <summary>
/// Single registration point for all Catalog Infrastructure services.
/// Call <see cref="AddInfrastructure"/> from <c>Catalog.Api</c>'s composition root.
/// </summary>
/// <remarks>
/// Configuration is consumed here, once, during service registration — no runtime
/// code reads <c>IConfiguration</c> directly after this method returns (§6).
///
/// <b>Lifetime decisions (§7, ADR 0005):</b>
/// <list type="bullet">
///   <item>
///     <c>IMongoClient</c> → Singleton.  The MongoDB driver explicitly documents
///     <c>MongoClient</c> as thread-safe and designed to be shared across the
///     application's lifetime — it manages its own connection pool.  Registering
///     it as anything other than Singleton would create a new connection pool per
///     scope, which wastes connections and undermines the driver's pooling design.
///   </item>
///   <item>
///     <c>IMongoDatabase</c> → Singleton.  <c>IMongoDatabase</c> is a lightweight
///     handle over <c>IMongoClient</c> with no per-request state; Singleton is safe.
///   </item>
///   <item>
///     <c>IMongoCollection&lt;Title&gt;</c> → Singleton.  Same reasoning as
///     <c>IMongoDatabase</c> — the collection handle holds no request-scoped state.
///     Registered as Singleton so both the write and read repositories resolve the
///     same handle without a factory allocation per request.
///   </item>
///   <item>
///     <c>CatalogWriteRepository</c> and <c>CatalogReadRepository</c> → Scoped.
///     They hold no per-request state, but Scoped lifetime matches every other
///     repository in this codebase, per ADR 0005's explicit note on this trade-off.
///   </item>
/// </list>
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Catalog Infrastructure services: MongoDB client, collection,
    /// repositories, and BSON configuration.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to bind <see cref="MongoDbOptions"/> from configuration.
    /// Not stored or accessed again after this method returns (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind MongoDb options section — all MongoDB config flows through this
        // strongly-typed class; nothing reads IConfiguration["MongoDb:*"] directly (§6).
        services.Configure<MongoDbOptions>(
            configuration.GetSection(MongoDbOptions.SectionName));

        // BSON class maps and serializers must be registered before the first
        // IMongoCollection<Title> is resolved.  Static registration is safe here
        // because DI registration is single-threaded at startup.
        CatalogBsonConfiguration.Register();

        // Singleton — MongoClient manages its own connection pool (see remarks above).
        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException(
                    $"Missing required configuration: {MongoDbOptions.SectionName}:{nameof(MongoDbOptions.ConnectionString)}.");
            }

            return new MongoClient(options.ConnectionString);
        });

        // Singleton — lightweight handle, no per-request state.
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client  = sp.GetRequiredService<IMongoClient>();
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return client.GetDatabase(options.DatabaseName);
        });

        // Singleton — collection handle is stateless; see remarks above.
        services.AddSingleton<IMongoCollection<Title>>(sp =>
        {
            var db      = sp.GetRequiredService<IMongoDatabase>();
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return db.GetCollection<Title>(options.TitlesCollectionName);
        });

        // Scoped repositories — match the lifetime convention of every other
        // repository in this codebase (ADR 0005).
        services.AddScoped<ICatalogWriteRepository, CatalogWriteRepository>();
        services.AddScoped<ICatalogReadRepository,  CatalogReadRepository>();

        return services;
    }

    /// <summary>
    /// Applies MongoDB indexes idempotently.  Call this from the application's
    /// startup sequence (e.g. after <c>builder.Build()</c>, before <c>app.Run()</c>).
    /// </summary>
    /// <remarks>
    /// Separated from <see cref="AddInfrastructure"/> because index creation is an
    /// async I/O operation and DI registration must be synchronous.  The split also
    /// makes it explicit that this is a startup side effect, not a service registration.
    /// </remarks>
    public static async Task ApplyIndexesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var collection = serviceProvider.GetRequiredService<IMongoCollection<Title>>();
        await TitleIndexes.ApplyAsync(collection, cancellationToken).ConfigureAwait(false);
    }
}
