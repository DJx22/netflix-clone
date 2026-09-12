using Serilog.Context;

namespace Streaming.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation ID in the log context (§13).
/// If the caller supplies <c>X-Correlation-Id</c> it is used as-is; otherwise
/// a new GUID is generated. The resolved ID is echoed in the response header
/// and pushed into Serilog's LogContext so it appears in every log line for
/// that request.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>HTTP header name — matches the OpenAPI <c>X-Correlation-Id</c> parameter.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key under which the resolved ID is stored in <see cref="HttpContext.Items"/>.</summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    /// <summary>Initialises a new <see cref="CorrelationIdMiddleware"/>.</summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // LogContext scope lasts exactly as long as the request — Serilog picks it up
        // via .Enrich.FromLogContext() configured in Program.cs.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
