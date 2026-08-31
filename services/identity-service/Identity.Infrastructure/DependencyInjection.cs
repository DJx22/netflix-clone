using Identity.Application.Interfaces;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Options;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

/// <summary>
/// Registers all Infrastructure services.
/// Called once from Identity.Api/DependencyInjection.cs — no DI logic lives in Program.cs (§7).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options ---
        // Bind configuration sections to strongly-typed classes.
        // IConfiguration["key"] reads are forbidden outside Program.cs (§6).
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<ConnectionStringOptions>(
            configuration.GetSection(ConnectionStringOptions.SectionName));

        // --- EF Core ---
        // Scoped: one DbContext per HTTP request (§7).
        // Connection string read here because AddDbContext must resolve it at startup;
        // this is the narrowest exception to the "no IConfiguration outside Options" rule.
        var connectionString = configuration
            .GetSection(ConnectionStringOptions.SectionName)
            .GetValue<string>(nameof(ConnectionStringOptions.IdentityDb));

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        // --- Repositories (Scoped — share the DbContext) ---
        services.AddScoped<IUserRepository, UserRepository>();

        // --- Security (Singleton — stateless after construction) ---
        // IPasswordHasher: BCrypt.Net is thread-safe; work factor baked in at construction.
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // ITokenService: signing key derived once from config; immutable thereafter.
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
