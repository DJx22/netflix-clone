using System.Text;
using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Subscription.Api.Options;
using Subscription.Application.Services;
using Subscription.Application.Validators;
using Subscription.Infrastructure;
using Subscription.Infrastructure.Persistence;

namespace Subscription.Api;

/// <summary>
/// Single extension method that wires every service for the Subscription API.
/// §7: all registration lives here — nothing scattered across Program.cs.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all services required by the Subscription API.
    /// </summary>
    public static IServiceCollection AddSubscriptionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Infrastructure (DbContext, repositories, DateTimeProvider) ---
        services.AddInfrastructure(configuration);

        // --- Application services ---
        // Plain service classes — no Mediator; reads and writes share one shape (Application plan).
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPlanService, PlanService>();

        // --- Validation (FluentValidation) ---
        // Auto-validation intercepts [FromBody] models before the action runs (§12).
        // Validators are sourced from the Application assembly.
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<CreateSubscriptionRequestValidator>();

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
                // This must match the claim-reading pattern in SubscriptionsController.GetAccountId().
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
                    // Sub claim maps to User.Identity.Name — consistent with Identity and Profile services.
                    NameClaimType            = JwtRegisteredClaimNames.Sub,
                    // Zero skew: a token that is one second past expiry is rejected.
                    ClockSkew                = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // --- Health checks (§11: readiness probe checks DB reachability, not just process alive) ---
        services.AddHealthChecks()
            .AddDbContextCheck<SubscriptionDbContext>(
                name: "subscription-db",
                tags: ["ready"]);

        // --- Controllers + OpenAPI ---
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}
