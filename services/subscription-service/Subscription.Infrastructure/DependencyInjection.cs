using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subscription.Application.Abstractions;
using Subscription.Application.Services;
using Subscription.Domain.Interfaces;
using Subscription.Infrastructure.Messaging;
using Subscription.Infrastructure.Options;
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
    /// Registers Infrastructure services: DbContext, repositories, clock provider,
    /// and the Phase 4 RabbitMQ PaymentCompleted consumer.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to bind options and wire <see cref="SubscriptionDbContext"/>
    /// with a connection string.  Configuration reads are confined to this
    /// composition-root method — no runtime infrastructure code accesses
    /// <c>IConfiguration</c> directly (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────────
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

        // ── Application services ──────────────────────────────────────────────
        // Registered here so the consumer's IServiceScopeFactory-resolved scope
        // can resolve ISubscriptionService → SubscriptionService.
        // ISubscriptionService is also registered in Subscription.Api.DependencyInjection
        // (which calls AddInfrastructure first, so this registration runs once).
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        // ── Phase 4: RabbitMQ consumer ────────────────────────────────────────
        // RabbitMq section: host / port / credentials / exchange / queue / DLQ names.
        // Defaults point at localhost:5672; Docker Compose overrides RabbitMq__Host=rabbitmq.
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        // PaymentCompletedConsumer is a BackgroundService (Singleton-scoped by the host).
        // It resolves ISubscriptionService per message via IServiceScopeFactory (§7).
        services.AddHostedService<PaymentCompletedConsumer>();

        return services;
    }
}
