namespace Fluxorq.Abstractions;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> and returns a response of type <typeparamref name="TResponse"/>.
/// Register implementations in the DI container via <c>AddFluxorq()</c>.
/// </summary>
/// <typeparam name="TRequest">The request type to handle.</typeparam>
/// <typeparam name="TResponse">The response type produced by this handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the given request and returns the response.</summary>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
