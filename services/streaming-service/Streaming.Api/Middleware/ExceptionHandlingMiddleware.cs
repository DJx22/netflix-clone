using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Streaming.Api.Middleware;

/// <summary>
/// Global exception handler (§10). Maps every thrown exception to a consistent
/// RFC 7807 ProblemDetails response (openapi.yaml ProblemDetails / ValidationProblemDetails schemas).
/// <para>
/// Expected failure paths in the Streaming service use typed results
/// (<c>GetMediaResult</c>, <c>GetPositionResult</c>) rather than exceptions, so this
/// middleware handles only genuinely unexpected failures (argument guards, validation,
/// and infrastructure faults) — never the normal 404 media / position paths.
/// </para>
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Initialises a new <see cref="ExceptionHandlingMiddleware"/>.</summary>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            // 400 — FluentValidation intercepted by auto-validation filter
            FluentValidation.ValidationException e
                => (HttpStatusCode.BadRequest, "Validation Failed",
                    string.Join("; ", e.Errors.Select(err => err.ErrorMessage))),

            // 400 — domain argument guard (e.g. negative positionSeconds, empty titleId)
            ArgumentException e
                => (HttpStatusCode.BadRequest, "Bad Request", e.Message),

            // 500 — unexpected; log stack trace, return nothing sensitive
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error",
                  "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            // Log full exception for bugs and infrastructure failures.
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            // Domain/argument failures are expected — warn level, no stack trace.
            _logger.LogWarning("Application exception {ExceptionType}: {Message}",
                exception.GetType().Name, exception.Message);
        }

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        var problem = new ProblemDetails
        {
            Type     = $"https://httpstatuses.com/{(int)statusCode}",
            Title    = title,
            Status   = (int)statusCode,
            Detail   = detail,
            Instance = context.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
