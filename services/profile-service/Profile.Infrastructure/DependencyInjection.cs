using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Profile.Domain.Repositories;
using Profile.Infrastructure.Options;
using Profile.Infrastructure.Persistence;
using Profile.Infrastructure.Persistence.Repositories;

namespace Profile.Infrastructure;

/// <summary>
/// Registers all Infrastructure services.
/// Called once from Profile.Api/DependencyInjection.cs — no DI logic lives in Program.cs (§7).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddProfileInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options ---
        // Bind configuration sections to strongly-typed classes.
        // IConfiguration["key"] reads are forbidden outside Program.cs (§6).
        services.Configure<ConnectionStringOptions>(
            configuration.GetSection(ConnectionStringOptions.SectionName));

        // --- EF Core ---
        // Scoped: one DbContext per HTTP request (§7).
        // Connection string read here because AddDbContext must resolve it at startup;
        // this is the narrowest exception to the "no IConfiguration outside Options" rule.
        var connectionString = configuration
            .GetSection(ConnectionStringOptions.SectionName)
            .GetValue<string>(nameof(ConnectionStringOptions.ProfileDb));

        services.AddDbContext<ProfileDbContext>(options =>
            options.UseSqlServer(connectionString));

        // --- Repositories (Scoped — share the DbContext) ---
        services.AddScoped<IProfileRepository, ProfileRepository>();

        return services;
    }
}
