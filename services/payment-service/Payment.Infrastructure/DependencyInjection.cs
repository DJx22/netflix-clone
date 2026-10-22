using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Abstractions;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Options;
using Payment.Infrastructure.Persistence;
using Payment.Infrastructure.Persistence.Repositories;
using Payment.Infrastructure.Services;

namespace Payment.Infrastructure;

/// <summary>
/// Single registration point for all Infrastructure services.
/// Call <see cref="AddInfrastructure"/> from the Api project's composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services: DbContext, repositories, clock, simulator,
    /// and the Phase 4 RabbitMQ event publisher.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to bind options and wire <see cref="PaymentDbContext"/> with a
    /// connection string.  Configuration reads are confined to this composition-root
    /// method — no runtime infrastructure code accesses <c>IConfiguration</c>
    /// directly (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options ───────────────────────────────────────────────────────────
        // RabbitMq section: host / port / credentials / exchange name.
        // Defaults point at localhost:5672 so no env var is needed in local dev.
        // Docker Compose overrides: RabbitMq__Host=rabbitmq (service-name DNS).
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException(
                "Missing required configuration: ConnectionStrings:PaymentDb.");

        // Scoped DbContext — accumulates change-tracked state per HTTP request (§7).
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Scoped — holds a reference to the scoped DbContext above (§7).
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // ── Stateless services (Singleton — no per-request state) ─────────────
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPaymentSimulator, PaymentSimulator>();

        // ── Phase 4: RabbitMQ event publisher ─────────────────────────────────
        // Singleton — holds one long-lived IConnection to the broker.
        // IHttpContextAccessor is injected to propagate X-Correlation-Id.
        // IAsyncDisposable: DI container calls DisposeAsync on shutdown.
        services.AddHttpContextAccessor();
        services.AddSingleton<RabbitMqEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());

        return services;
    }
}
