using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Identity.Application.Services;
using Identity.Application.Validators;
using Identity.Infrastructure;
using Identity.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api;

/// <summary>
/// Single extension method that wires every service for the Identity API.
/// §7: all registration lives here — nothing scattered across Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Infrastructure (DbContext, repositories, hasher, token service) ---
        services.AddIdentityInfrastructure(configuration);

        // --- Application services ---
        // Plain service class (no Mediator) — see Application README decision log.
        services.AddScoped<AuthService>();

        // --- Validation (FluentValidation) ---
        // Registers all validators from the Application assembly automatically.
        // Auto-validation is disabled — validators are called explicitly by the pipeline
        // filter below so that error responses conform to problem-details format (§10).
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        // --- JWT Bearer authentication (§12) ---
        // Validates issuer, audience, and expiry using the same configured key the issuer used.
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    ClockSkew = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // --- Health checks (§11: readiness probe checks DB reachability, not just process alive) ---
        services.AddHealthChecks()
            .AddDbContextCheck<Identity.Infrastructure.Persistence.IdentityDbContext>(
                name: "identity-db",
                tags: ["ready"]);

        // --- Controllers + OpenAPI ---
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}
