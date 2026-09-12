using System.Text;
using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Streaming.Api.Options;
using Streaming.Application.Validators;
using Streaming.Infrastructure;
using Streaming.Infrastructure.Persistence;

namespace Streaming.Api;

/// <summary>
/// Single extension method that wires every service for the Streaming API.
/// §7: all registration lives here — nothing scattered across Program.cs.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all services required by the Streaming API.
    /// </summary>
    public static IServiceCollection AddStreamingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Infrastructure (DbContext, repos, IPlaybackService, CatalogHttpClient, DateTimeProvider) ---
        services.AddInfrastructure(configuration);

        // --- Validation (FluentValidation) ---
        // Auto-validation intercepts [FromBody] models before the action runs (§12).
        // Validator is sourced from the Application assembly.
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<SavePositionRequestValidator>();

        // --- JWT Bearer authentication (§12) ---
        // Validates issuer, audience, and expiry using the same key Identity signed with.
        // Must match Identity.Api's token-issuance configuration exactly.
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

                // MapInboundClaims = false preserves the original claim names from the JWT
                // (e.g. "sub") instead of mapping them to long .NET claim type URIs.
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
                    // Sub claim maps to User.Identity.Name — consistent with Identity and Subscription services.
                    NameClaimType            = JwtRegisteredClaimNames.Sub,
                    // Zero skew: a token that is one second past expiry is rejected.
                    ClockSkew                = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // --- Health checks (§11: readiness probe checks DB reachability, not just process alive) ---
        services.AddHealthChecks()
            .AddDbContextCheck<StreamingDbContext>(
                name: "streaming-db",
                tags: ["ready"]);

        // --- Controllers + OpenAPI ---
        services.AddControllers();
        services.AddOpenApi();

        // --- Swagger UI (development exploration, §15) ---
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}

