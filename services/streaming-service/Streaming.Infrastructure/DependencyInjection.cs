using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Streaming.Application.Abstractions;
using Streaming.Application.Services;
using Streaming.Domain.Interfaces;
using Streaming.Infrastructure.ExternalClients;
using Streaming.Infrastructure.Options;
using Streaming.Infrastructure.Persistence;
using Streaming.Infrastructure.Persistence.Repositories;
using Streaming.Infrastructure.Services;

namespace Streaming.Infrastructure;

/// <summary>
/// Single registration point for all Infrastructure services.
/// Call <see cref="AddInfrastructure"/> from the Api project's composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services: DbContext, repositories, clock provider,
    /// and the Catalog HTTP client.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to read connection strings and client base URLs.
    /// Configuration reads are confined to this composition-root method — no runtime
    /// infrastructure code accesses <c>IConfiguration</c> directly (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options ---
        services.Configure<ConnectionStringOptions>(
            configuration.GetSection("ConnectionStrings"));

        services.Configure<CatalogClientOptions>(
            configuration.GetSection("CatalogClient"));

        // --- Database ---
        var connectionString = configuration.GetConnectionString("StreamingDb")
            ?? throw new InvalidOperationException(
                "Missing required configuration: ConnectionStrings:StreamingDb.");

        // Scoped DbContext — accumulates change-tracked state per HTTP request (§7).
        services.AddDbContext<StreamingDbContext>(options =>
            options.UseSqlServer(connectionString));

        // --- Repositories (Scoped — hold a reference to the Scoped DbContext above) ---
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddScoped<IPlaybackPositionRepository, PlaybackPositionRepository>();

        // --- Application service ---
        services.AddScoped<IPlaybackService, PlaybackService>();

        // --- Clock provider (Singleton — stateless) ---
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // --- Catalog HTTP client ---
        // Reads BaseUrl from CatalogClientOptions at registration time.
        var catalogBaseUrl = configuration["CatalogClient:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Missing required configuration: CatalogClient:BaseUrl.");

        // AddHttpClient<T> registers the typed client with a managed HttpClientHandler.
        // ICatalogClient is mapped separately so Application-layer code only sees the interface.
        services
            .AddHttpClient<CatalogHttpClient>(client =>
            {
                client.BaseAddress = new Uri(catalogBaseUrl.TrimEnd('/') + "/");
            });

        // Map the interface → concrete type so ICatalogClient resolves from DI.
        services.AddScoped<ICatalogClient, CatalogHttpClient>();

        return services;
    }
}
