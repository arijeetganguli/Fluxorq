namespace Fluxorq.Abstractions;

/// <summary>
/// Dispatches requests to their registered handlers, executing all pipeline behaviors in order.
/// Inject <see cref="IDispatcher"/> wherever you need to trigger request handling.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Sends a request through the pipeline and returns the response produced by the handler.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type; inferred from the request.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The response produced by the handler.</returns>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
