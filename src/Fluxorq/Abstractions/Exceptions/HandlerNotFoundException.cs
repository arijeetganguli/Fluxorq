namespace Fluxorq.Abstractions.Exceptions;

/// <summary>
/// Thrown by the <see cref="Core.Dispatcher"/> when no handler is registered for
/// the dispatched request type.
/// </summary>
public sealed class HandlerNotFoundException : Exception
{
    /// <summary>The request type for which no handler could be resolved.</summary>
    public Type RequestType { get; }

    public HandlerNotFoundException(Type requestType)
        : base($"No handler registered for request type '{requestType.FullName}'. " +
               $"Ensure a class implementing IRequestHandler<{requestType.Name}, TResponse> " +
               $"is registered in the DI container.")
    {
        RequestType = requestType;
    }
}
