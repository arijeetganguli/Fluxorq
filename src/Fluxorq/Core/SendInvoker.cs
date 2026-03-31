using System.Collections.Concurrent;
using Fluxorq.Abstractions;
using Fluxorq.Abstractions.Exceptions;

namespace Fluxorq.Core;

/// <summary>
/// Builds and caches one invoker per concrete request type.
/// The cache is keyed on request type and stores entries as <c>object</c> so a single
/// dictionary can serve all response types without generic constraints on the cache itself.
/// Callers cast to <c>RequestInvoker&lt;TResponse&gt;</c> — the cast is guaranteed correct
/// by construction because the invoker was created with the matching response type.
/// </summary>
internal static class SendInvokerCache
{
    private static readonly ConcurrentDictionary<Type, object> _cache = new();

    internal static RequestInvoker<TResponse> GetOrCreate<TResponse>(Type requestType)
    {
        return (RequestInvoker<TResponse>)_cache.GetOrAdd(
            requestType,
            static (rt, respType) =>
                Activator.CreateInstance(typeof(RequestInvokerImpl<,>).MakeGenericType(rt, respType))!,
            typeof(TResponse));
    }
}

/// <summary>
/// Abstract base typed on <typeparamref name="TResponse"/> only.
/// Stored in the cache as <c>object</c>; retrieved and cast to <see cref="RequestInvoker{TResponse}"/>
/// by the <see cref="Dispatcher"/>.
///
/// This design eliminates all boxing that the previous <c>ISendInvoker → Task&lt;object?&gt;</c>
/// approach required: <typeparamref name="TResponse"/> flows through without being widened to
/// <c>object</c>, and no extra <c>Task&lt;object?&gt;</c> wrapper is allocated per dispatch.
/// </summary>
internal abstract class RequestInvoker<TResponse>
{
    internal abstract Task<TResponse> InvokeAsync(
        IServiceProvider serviceProvider,
        IRequest<TResponse> request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Concrete invoker for a <typeparamref name="TRequest"/> / <typeparamref name="TResponse"/> pair.
///
/// Performance design:
/// <list type="bullet">
///   <item>Not <c>async</c> — returns <c>Task&lt;TResponse&gt;</c> directly, zero state-machine overhead.</item>
///   <item>Zero-behavior fast path — returns <c>handler.HandleAsync()</c> as-is; no allocations beyond the DI resolution.</item>
///   <item><see cref="PipelineRunner"/> — one object + one bound delegate for the full behavior chain.</item>
///   <item>Static service-type fields — one field load per call, no type-token instruction.</item>
/// </list>
/// </summary>
internal sealed class RequestInvokerImpl<TRequest, TResponse> : RequestInvoker<TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Type _handlerType  = typeof(IRequestHandler<TRequest, TResponse>);
    private static readonly Type _behaviorType = typeof(IEnumerable<IPipelineBehavior<TRequest, TResponse>>);

    internal override Task<TResponse> InvokeAsync(
        IServiceProvider serviceProvider,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService(_handlerType) is not IRequestHandler<TRequest, TResponse> handler)
            throw new HandlerNotFoundException(request.GetType());

        var behaviors = ResolveBehaviors(serviceProvider);

        // Zero-behavior fast path: tail-returns the handler's Task directly.
        // No async wrapper, no PipelineRunner, no extra allocation beyond DI resolution.
        if (behaviors.Length == 0)
            return handler.HandleAsync((TRequest)request, cancellationToken);

        return new PipelineRunner(handler, behaviors, (TRequest)request, cancellationToken).NextAsync();
    }

    /// <summary>
    /// Resolves the behavior collection from DI.
    /// Microsoft.Extensions.DependencyInjection delivers open-generic enumerables as <c>T[]</c>,
    /// so the array branch is the common case and avoids a <c>ToArray()</c> copy.
    /// </summary>
    private static IPipelineBehavior<TRequest, TResponse>[] ResolveBehaviors(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService(_behaviorType) switch
        {
            IPipelineBehavior<TRequest, TResponse>[] arr => arr,
            IEnumerable<IPipelineBehavior<TRequest, TResponse>> seq => seq.ToArray(),
            _ => []
        };
    }

    /// <summary>
    /// Walks the behavior array by integer index.
    /// One <see cref="PipelineRunner"/> + one bound <see cref="NextDelegate{TResponse}"/> covers
    /// the whole chain, regardless of how many behaviors are registered.
    /// </summary>
    private sealed class PipelineRunner
    {
        private readonly IRequestHandler<TRequest, TResponse> _handler;
        private readonly IPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly TRequest _request;
        private readonly CancellationToken _cancellationToken;
        private readonly NextDelegate<TResponse> _next;
        private int _index;

        internal PipelineRunner(
            IRequestHandler<TRequest, TResponse> handler,
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            TRequest request,
            CancellationToken cancellationToken)
        {
            _handler = handler;
            _behaviors = behaviors;
            _request = request;
            _cancellationToken = cancellationToken;
            _next = NextAsync;
        }

        internal Task<TResponse> NextAsync()
        {
            if (_index < _behaviors.Length)
                return _behaviors[_index++].HandleAsync(_request, _next, _cancellationToken);

            return _handler.HandleAsync(_request, _cancellationToken);
        }
    }
}

