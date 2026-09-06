using System.Net;
using System.Text.Json;
using Catalog.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Middleware;

/// <summary>
/// Global exception handler (§10).  Maps every thrown exception to a consistent
/// RFC 7807 problem-details response, matching the OpenAPI spec's
/// <c>ProblemDetails</c> and <c>ValidationProblemDetails</c> schemas.
/// </summary>
/// <remarks>
/// <b>Expected failures</b> (domain or validation exceptions) are mapped to
/// their correct HTTP status codes.  Logs at Warning — no stack trace, because
/// these paths are not bugs.
///
/// <b>Unexpected failures</b> fall through to 500.  Logs at Error with the full
/// exception so it is traceable in Seq via the correlation ID (§13).
/// The 500 response body contains no internal detail — stack traces and
/// exception messages must never reach the client (§12, §16).
///
/// Catch-all behaviour is intentional: §10 and §16 both forbid
/// <c>catch (Exception)</c> with no rethrow or log.  Here, the log *is* the
/// rethrow equivalent — the exception is fully captured and then the response
/// is terminal, so rethrowing would bypass the response that was just written.
/// </remarks>
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
        _next   = next;
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
        var (statusCode, title, detail, errors) = MapException(exception);

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            // Full exception logged here — never send it to the client (§12, §16).
            _logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            // Expected domain/validation paths are not bugs — warn without stack trace.
            _logger.LogWarning(
                "Domain exception {ExceptionType}: {Message}",
                exception.GetType().Name,
                exception.Message);
        }

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        if (errors is not null)
        {
            // ValidationProblemDetails — matches the openapi.yaml ValidationProblemDetails schema.
            var validationProblem = new ValidationProblemDetails(errors)
            {
                Type     = $"https://httpstatuses.com/{(int)statusCode}",
                Title    = title,
                Status   = (int)statusCode,
                Detail   = detail,
                Instance = context.Request.Path,
            };
            validationProblem.Extensions["correlationId"] = correlationId;

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(validationProblem, JsonOptions));
        }
        else
        {
            var problem = new ProblemDetails
            {
                Type     = $"https://httpstatuses.com/{(int)statusCode}",
                Title    = title,
                Status   = (int)statusCode,
                Detail   = detail,
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(problem, JsonOptions));
        }
    }

    private static (
        HttpStatusCode StatusCode,
        string Title,
        string Detail,
        Dictionary<string, string[]>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            // 404 — a title with the requested ID does not exist
            TitleNotFoundException e =>
                (HttpStatusCode.NotFound, "Not Found", e.Message, null),

            // 400 — a field value violates a domain invariant (e.g. blank name,
            //        year out of range). Surfaces as a single-field error dict
            //        matching ValidationProblemDetails so callers see which field failed.
            TitleValidationException e =>
                (HttpStatusCode.BadRequest, "Validation Failed", e.Message,
                 new Dictionary<string, string[]>
                 {
                     [e.FieldName] = [e.Message]
                 }),

            // 400 — FluentValidation failure from the ValidationBehaviour pipeline.
            //        Expands all field errors into the errors dictionary.
            ValidationException e =>
                (HttpStatusCode.BadRequest, "Validation Failed",
                 "One or more fields failed validation.",
                 e.Errors
                     .GroupBy(f => f.PropertyName)
                     .ToDictionary(
                         g => g.Key,
                         g => g.Select(f => f.ErrorMessage).ToArray())),

            // 500 — unexpected; internal detail stays in the log, not the response
            _ =>
                (HttpStatusCode.InternalServerError, "Internal Server Error",
                 "An unexpected error occurred. Please try again later.", null)
        };
    }
}
