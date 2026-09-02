using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Subscription.Application.Exceptions;
using Subscription.Domain.Exceptions;

namespace Subscription.Api.Middleware;

/// <summary>
/// Global exception handler (§10). Maps every thrown exception to a consistent
/// RFC 7807 problem-details response (openapi.yaml ProblemDetails schema).
/// <para>
/// Expected failures (not-found, duplicate subscription, invalid state transition)
/// are typed domain or application exceptions — this middleware maps them to their
/// correct HTTP status codes. Genuinely unexpected exceptions fall through to 500,
/// with full context in the structured log but no internal detail in the response body.
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
            // 404 — resource not found
            SubscriptionNotFoundException e
                => (HttpStatusCode.NotFound, "Not Found", e.Message),
            PlanNotFoundException e
                => (HttpStatusCode.NotFound, "Not Found", e.Message),

            // 409 — state conflict: account already has an active or pending subscription
            DuplicateSubscriptionException e
                => (HttpStatusCode.Conflict, "Conflict", e.Message),

            // 412 — optimistic concurrency conflict; client should re-fetch and retry
            DbUpdateConcurrencyException
                => (HttpStatusCode.PreconditionFailed, "Precondition Failed",
                    "The subscription was modified concurrently. Fetch the current state and retry."),

            // 422 — request was structurally valid but violates a business rule given current state
            InvalidSubscriptionOperationException e
                => (HttpStatusCode.UnprocessableEntity, "Unprocessable Entity", e.Message),

            // 400 — DTO-level validation failure (FluentValidation called manually in service code)
            FluentValidation.ValidationException e
                => (HttpStatusCode.BadRequest, "Validation Failed",
                    string.Join("; ", e.Errors.Select(err => err.ErrorMessage))),

            // 400 — domain argument guard
            ArgumentException e
                => (HttpStatusCode.BadRequest, "Bad Request", e.Message),

            // 500 — unexpected; log stack trace, return nothing sensitive
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error",
                  "An unexpected error occurred. Please try again later.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            // Log full exception for bugs and infra failures — never for expected domain paths.
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            // Domain/application failures are expected — warn level, no stack trace in log.
            _logger.LogWarning("Domain exception {ExceptionType}: {Message}",
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
