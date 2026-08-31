namespace Identity.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation ID (§13).
/// If the caller supplies X-Correlation-Id it is used as-is; otherwise one is
/// generated. The resolved ID is echoed in the response header and pushed into
/// Serilog's LogContext so it appears in every log line for that request.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key under which the resolved ID is stored in HttpContext.Items.</summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
