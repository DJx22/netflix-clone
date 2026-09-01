using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Profile.Application.Services;
using Profile.Application.Validators;
using Profile.Api.Options;
using Profile.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Profile.Api;

/// <summary>
/// Single extension method that wires every service for the Profile API.
/// §7: all registration lives here — nothing scattered across Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddProfileServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Infrastructure (DbContext, repositories) ---
        services.AddProfileInfrastructure(configuration);

        // --- Application services ---
        // Plain service class (no Mediator) — reads and writes share the same shape.
        services.AddScoped<ProfileService>();

        // --- Validation (FluentValidation) ---
        // Registers all validators from the Application assembly automatically.
        // Auto-validation is disabled — validators are called explicitly by the pipeline
        // filter below so that error responses conform to problem-details format (§10).
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<CreateProfileRequestValidator>();

        // --- JWT Bearer authentication (§12) ---
        // Validates issuer, audience, and expiry using the same configured key Identity signed with.
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

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
            .AddDbContextCheck<Profile.Infrastructure.Persistence.ProfileDbContext>(
                name: "profile-db",
                tags: ["ready"]);

        // --- Controllers + OpenAPI ---
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}
