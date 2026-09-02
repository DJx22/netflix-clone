using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subscription.Application.Abstractions;
using Subscription.Domain.Interfaces;
using Subscription.Infrastructure.Persistence;
using Subscription.Infrastructure.Services;

namespace Subscription.Infrastructure;

/// <summary>
/// Single registration point for all Infrastructure services.
/// Call <see cref="AddInfrastructure"/> from the Api project's composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services: DbContext, repositories, and the clock provider.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to wire <see cref="SubscriptionDbContext"/> with a connection string.
    /// Configuration reads are confined to this composition-root method — no runtime
    /// infrastructure code accesses <c>IConfiguration</c> directly (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SubscriptionDb")
            ?? throw new InvalidOperationException(
                "Missing required configuration: ConnectionStrings:SubscriptionDb.");

        // Scoped DbContext — accumulates change-tracked state per HTTP request (§7).
        services.AddDbContext<SubscriptionDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Scoped — hold a reference to the scoped DbContext above (§7).
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();

        // Singleton — no state; returns DateTime.UtcNow on every call (§7).
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
