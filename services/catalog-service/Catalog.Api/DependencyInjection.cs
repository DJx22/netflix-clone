using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Catalog.Api.Options;
using Catalog.Application.Behaviours;
using Catalog.Application.Validators;
using Catalog.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace Catalog.Api;

/// <summary>
/// Single extension method that wires every service for the Catalog API.
/// §7: all registration lives here — nothing scattered across Program.cs.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all services required by the Catalog API.
    /// Configuration is consumed once here and not accessed at runtime (§6).
    /// </summary>
    public static IServiceCollection AddCatalogServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Infrastructure (MongoClient, repositories, BSON configuration) ---
        services.AddInfrastructure(configuration);

        // --- MediatR — command and query dispatch (§6 CQRS) ---
        // Handlers are sourced from the Application assembly via assembly scanning.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<CreateTitleCommandValidator>());

        // --- MediatR pipeline behaviours (order matters: logging wraps validation) ---
        // LoggingBehaviour runs first (outer) — it captures total elapsed time including validation.
        // ValidationBehaviour runs second (inner) — it rejects invalid requests before handlers run.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        // --- FluentValidation — validators sourced from Application assembly (§12) ---
        // AddFluentValidationAutoValidation wires [FromBody] model validation for controllers.
        // Validators in the MediatR pipeline run independently via ValidationBehaviour.
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<CreateTitleCommandValidator>();

        // --- JWT Bearer authentication (§12) ---
        // Validates issuer, audience, and expiry using the key Identity signs tokens with.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration
                    .GetSection(JwtOptions.SectionName)
                    .Get<JwtOptions>() ?? new JwtOptions();

                if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
                {
                    throw new InvalidOperationException(
                        "Jwt:Secret must be configured before the API can validate bearer tokens.");
                }

                // MapInboundClaims = false preserves original JWT claim names ("sub", not the
                // long .NET URI form) — must match how controllers read the sub claim.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtOptions.Issuer,
                    ValidAudience            = jwtOptions.Audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    NameClaimType            = JwtRegisteredClaimNames.Sub,
                    // Zero skew: a token one second past expiry is rejected immediately.
                    ClockSkew                = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // --- Health checks (§11: readiness = MongoDB reachable, not just process alive) ---
        // Resolves IMongoDatabase from DI (the Singleton registered by AddInfrastructure) and
        // runs a ping command against it.  A failed ping returns 503 from HealthController.
        // Using the Func<IServiceProvider, IMongoDatabase> overload because the v9 package
        // no longer accepts a raw connection string — it requires an already-constructed client.
        services.AddHealthChecks()
            .AddMongoDb(
                sp => sp.GetRequiredService<IMongoDatabase>(),
                name: "catalog-mongo",
                tags: ["ready"]);

        // --- Controllers + OpenAPI ---
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}
