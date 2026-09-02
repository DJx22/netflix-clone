using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Payment.Application.Abstractions;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Options;
using Payment.Infrastructure.Persistence;
using Payment.Infrastructure.Persistence.Repositories;
using Payment.Infrastructure.Services;
using Polly;
using Polly.Extensions.Http;

namespace Payment.Infrastructure;

/// <summary>
/// Single registration point for all Infrastructure services.
/// Call <see cref="AddInfrastructure"/> from the Api project's composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services: DbContext, repositories, clock, simulator,
    /// and the ADR 0003 Subscription activation HTTP client.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">
    /// Used once here to wire <see cref="PaymentDbContext"/> with a connection string.
    /// Configuration reads are confined to this composition-root method — no runtime
    /// infrastructure code accesses <c>IConfiguration</c> directly (§6).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options ───────────────────────────────────────────────────────────
        services.Configure<SubscriptionServiceOptions>(
            configuration.GetSection("SubscriptionService"));

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

        // ── ADR 0003 HTTP bridge: Payment → Subscription activate ─────────────
        // Scoped wrapper — holds a logger that carries per-request scope state (§7).
        services.AddScoped<IEventPublisher, SubscriptionActivationClient>();

        // Named HttpClient: BaseAddress from options, Polly retry + circuit-breaker (§6).
        // IHttpClientFactory manages the underlying HttpMessageHandler pool (Singleton lifetime
        // in the pool); SubscriptionActivationClient is Scoped and creates a new logical
        // HttpClient per request via CreateClient() — this is the correct IHttpClientFactory
        // pattern and avoids the DNS-refresh problem of a long-lived HttpClient field.
        services
            .AddHttpClient(SubscriptionActivationClient.HttpClientName, (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<SubscriptionServiceOptions>>()
                    .Value;

                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout     = TimeSpan.FromSeconds(10);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    // ── Polly policies ────────────────────────────────────────────────────────

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        // 3 retries with exponential back-off: ~2 s, ~4 s, ~8 s.
        // Retries transient HTTP errors and 5xx responses.
        // Does not retry 4xx (client errors) — those are not transient.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        // Open the circuit after 5 consecutive failures; keep it open for 30 s.
        // Prevents a down Subscription service from blocking all Payment requests.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
