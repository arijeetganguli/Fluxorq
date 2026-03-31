namespace Fluxorq.Abstractions;

/// <summary>
/// A middleware component that wraps request handling. Behaviors execute in registration order,
/// each receiving the request and a delegate to invoke the next stage of the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type this behavior applies to.</typeparam>
/// <typeparam name="TResponse">The response type this behavior produces.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Called during pipeline execution. Invoke <paramref name="next"/> to continue;
    /// return a value without calling <paramref name="next"/> to short-circuit.
    /// </summary>
    Task<TResponse> HandleAsync(
        TRequest request,
        NextDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}
