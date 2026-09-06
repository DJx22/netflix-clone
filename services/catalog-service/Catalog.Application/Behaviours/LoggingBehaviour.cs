using MediatR;
using Microsoft.Extensions.Logging;

namespace Catalog.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that records the entry, exit, and elapsed time
/// for every command and query handled by the Catalog service.
/// </summary>
/// <remarks>
/// Logging in the pipeline rather than in individual handlers:
/// (a) keeps handler code free of observability concerns (§5 SRP),
/// (b) guarantees every future request type gets the same log entries automatically.
///
/// ILogger is injected here rather than through a static factory so the logger
/// category is scoped to the concrete request type, which makes filtering in Seq
/// straightforward (§13).
///
/// Warning-level log on slow requests is intentional: it surfaces performance
/// regressions in the catalog's read path without requiring a separate profiler.
/// </remarks>
public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Requests taking longer than this threshold are logged at Warning level so
    /// they surface in Seq without requiring a query — the catalog's read path is
    /// supposed to be fast (ADR 0005, ADR read projection rationale).
    /// </summary>
    private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var start = DateTimeOffset.UtcNow;

        try
        {
            var response = await next().ConfigureAwait(false);

            var elapsed = DateTimeOffset.UtcNow - start;

            if (elapsed > SlowRequestThreshold)
            {
                _logger.LogWarning(
                    "Slow request {RequestName} completed in {ElapsedMs}ms",
                    requestName,
                    elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMs}ms",
                    requestName,
                    elapsed.TotalMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            var elapsed = DateTimeOffset.UtcNow - start;

            // Log at Error so the failure is findable in Seq; rethrow so the
            // global exception middleware can map it to the correct HTTP response
            // without this behaviour swallowing the exception (§10, §16).
            _logger.LogError(
                ex,
                "Request {RequestName} failed after {ElapsedMs}ms",
                requestName,
                elapsed.TotalMilliseconds);

            throw;
        }
    }
}
