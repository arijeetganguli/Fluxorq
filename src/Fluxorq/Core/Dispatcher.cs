using Fluxorq.Abstractions;

namespace Fluxorq.Core;

/// <summary>
/// Default <see cref="IDispatcher"/> implementation.
///
/// Hot dispatch path: null-check → <c>GetType()</c> → dictionary lookup → invoker call.
/// No <c>async</c> keyword here — <see cref="SendAsync{TResponse}"/> returns the invoker's
/// <c>Task&lt;TResponse&gt;</c> directly, so the Dispatcher adds zero state-machine overhead.
///
/// The invoker itself is also non-async: it either returns the handler's task directly
/// (zero-behavior path) or a <c>PipelineRunner</c>-backed task — neither involves boxing
/// the response through <c>object</c>.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // GetOrCreate<TResponse> returns a RequestInvoker<TResponse> typed on TResponse —
        // no unboxing, no extra Task wrapper, no state machine on this stack frame.
        return SendInvokerCache
            .GetOrCreate<TResponse>(request.GetType())
            .InvokeAsync(_serviceProvider, request, cancellationToken);
    }
}

