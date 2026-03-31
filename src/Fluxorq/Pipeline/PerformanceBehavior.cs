using System.Diagnostics;
using Fluxorq.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fluxorq.Pipeline;

/// <summary>
/// Pipeline behavior that measures request execution time.
/// Emits a warning log when the elapsed time exceeds <see cref="SlowRequestThresholdMs"/>.
/// </summary>
/// <typeparam name="TRequest">The request type being timed.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Requests taking longer than this threshold will trigger a warning log.</summary>
    public const int SlowRequestThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        NextDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await next().ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            if (elapsed > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} completed in {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    typeof(TRequest).Name,
                    elapsed,
                    SlowRequestThresholdMs);
            }
        }
    }
}
