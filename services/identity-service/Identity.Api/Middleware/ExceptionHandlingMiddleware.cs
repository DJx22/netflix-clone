using System.Net;
using System.Text.Json;
using Identity.Application.Exceptions;
using Identity.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Middleware;

/// <summary>
/// Global exception handler (§10). Maps every thrown exception to a consistent
/// RFC 7807 problem-details response (openapi.yaml ProblemDetails schema).
///
/// Expected failures (validation errors, invalid tokens, wrong credentials) are
/// already typed exceptions from Application/Domain — this middleware just ensures
/// they reach the caller with the right status code and body without leaking stack traces.
///
/// Genuinely unexpected exceptions (DB connectivity, bugs) fall through to the
/// final catch and return 500, with full context in the structured log but
/// nothing sensitive in the response body.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

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
            EmailAlreadyRegisteredException => (HttpStatusCode.Conflict, "Conflict", exception.Message),
            InvalidCredentialsException => (HttpStatusCode.Unauthorized, "Unauthorized", exception.Message),
            InvalidRefreshTokenException => (HttpStatusCode.Unauthorized, "Unauthorized", exception.Message),
            UserNotFoundException => (HttpStatusCode.Unauthorized, "Unauthorized", exception.Message),

            // DomainException base — any domain rule violation not explicitly mapped above.
            DomainException => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),

            // Everything else is unexpected. Log with full context; return a generic 500.
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error",
                      "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            // Log stack trace for bugs/infra failures; never for expected domain exceptions.
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            // Domain failures are expected paths — warn level, no stack trace.
            _logger.LogWarning("Domain exception {ExceptionType}: {Message}",
                exception.GetType().Name, exception.Message);
        }

        var correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault()
            ?? context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}
