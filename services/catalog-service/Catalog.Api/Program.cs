using Catalog.Api;
using Catalog.Api.Middleware;
using Catalog.Infrastructure;
using Serilog;

// Bootstrap Serilog early so startup errors are captured in structured format (§13).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging via Serilog — replaces the default Microsoft logger (§13).
    // Configuration is read from appsettings so log levels/sinks can change per environment
    // without rebuilding.  Enrichers add MachineName and CorrelationId (pushed by middleware).
    builder.Host.UseSerilog((context, services, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()     // picks up CorrelationId pushed by CorrelationIdMiddleware
            .Enrich.WithMachineName()
            .WriteTo.Console(
                outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // All service registration in one place (§7).
    builder.Services.AddCatalogServices(builder.Configuration);

    // Swagger UI for development / local exploration.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // --- Middleware pipeline (order is significant) ---

    // 1. Correlation ID first — must be in LogContext before anything else logs.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2. Global exception handler — catches everything the pipeline throws (§10).
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 3. OpenAPI + Swagger (development / local only).
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 4. Serilog request logging — logs after correlation ID is in scope so the
    //    request log line carries the CorrelationId property.
    app.UseSerilogRequestLogging();

    // 5. Auth pipeline — must come before MapControllers.
    app.UseAuthentication();
    app.UseAuthorization();

    // 6. Route to controllers.
    app.MapControllers();

    // Apply MongoDB indexes idempotently before accepting traffic.
    // Separated from AddInfrastructure because DI registration is synchronous
    // but index creation is async I/O (see DependencyInjection in Infrastructure).
    await Catalog.Infrastructure.DependencyInjection.ApplyIndexesAsync(app.Services);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Capture fatal startup failures (misconfigured secret, missing connection string, etc.).
    Log.Fatal(ex, "Catalog.Api failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Expose the implicit Program class for integration test factories (§14).
public partial class Program { }
