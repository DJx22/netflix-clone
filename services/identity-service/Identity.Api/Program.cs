using Identity.Api;
using Identity.Api.Middleware;
using Serilog;

// Bootstrap Serilog early so startup errors are captured in structured format (§13).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging via Serilog — replaces the default Microsoft logging (§13).
    builder.Host.UseSerilog((context, services, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()           // picks up CorrelationId pushed by middleware
            .Enrich.WithMachineName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // All service registration in one place (§7).
    builder.Services.AddIdentityServices(builder.Configuration);

    // Optional Swagger/OpenAPI support for development (§15).
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // --- Middleware pipeline (order is significant) ---

    // 1. Correlation ID first — must be in LogContext before anything else logs.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2. Global exception handler — catches everything the pipeline throws (§10).
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 3. OpenAPI (development only).
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 4. Serilog request logging — logs after correlation ID is in scope.
    app.UseSerilogRequestLogging();

    // 5. Auth pipeline — must come before MapControllers.
    app.UseAuthentication();
    app.UseAuthorization();

    // 6. Route to controllers.
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Capture fatal startup failures (misconfigured secret, missing connection string, etc.).
    Log.Fatal(ex, "Identity.Api failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Expose the implicit Program class for integration test factories.
public partial class Program { }
