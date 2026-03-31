using Fluxorq.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fluxorq.Pipeline;

/// <summary>
/// Pipeline behavior that logs request dispatch entry, success, and error.
/// Runs as the outermost behavior by default (registered first).
/// </summary>
/// <typeparam name="TRequest">The request type being dispatched.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
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
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Dispatching {RequestName}", requestName);

        try
        {
            var response = await next().ConfigureAwait(false);
            _logger.LogInformation("Dispatched {RequestName} successfully", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching {RequestName}", requestName);
            throw;
        }
    }
}
