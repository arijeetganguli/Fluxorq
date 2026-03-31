namespace Fluxorq.Abstractions;

/// <summary>
/// Marks a type as a dispatchable request that produces a response of <typeparamref name="TResponse"/>.
/// Implement this interface on commands and queries to enable dispatch via <see cref="IDispatcher"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the value returned when this request is handled.</typeparam>
public interface IRequest<out TResponse> { }
