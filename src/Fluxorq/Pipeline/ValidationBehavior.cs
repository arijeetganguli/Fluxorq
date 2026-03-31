using Fluxorq.Abstractions;
using Fluxorq.Abstractions.Exceptions;

namespace Fluxorq.Pipeline;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{TRequest}"/> instances
/// before invoking the next stage. Throws <see cref="FluxorqValidationException"/> when
/// any validator reports failures.
///
/// If no validators are registered for <typeparamref name="TRequest"/> the behavior is a no-op.
/// </summary>
/// <typeparam name="TRequest">The request type being validated.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // Captured as an array at construction time so:
    //  1. The Length == 0 check is a single integer comparison (no LINQ Any() or GetEnumerator()).
    //  2. The loop uses a simple index without allocating an enumerator.
    // Microsoft.Extensions.DependencyInjection delivers IEnumerable<T> as T[] so the
    // cast succeeds in the common case and avoids the ToArray() copy.
    private readonly IValidator<TRequest>[] _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators as IValidator<TRequest>[] ?? validators.ToArray();
    }

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        NextDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        // Fast path — skip all validation work when no validators are registered.
        if (_validators.Length == 0)
            return await next().ConfigureAwait(false);

        // Imperative loop: avoids the Select/Where/SelectMany/ToList chain that
        // allocates three intermediate IEnumerable objects per dispatch even when
        // every validator passes.  `failures` is only allocated when there is
        // actually something to report.
        List<ValidationFailure>? failures = null;

        foreach (var validator in _validators)
        {
            var result = validator.Validate(request);
            if (!result.IsValid)
            {
                failures ??= new List<ValidationFailure>(result.Failures.Count);
                failures.AddRange(result.Failures);
            }
        }

        if (failures is { Count: > 0 })
            throw new FluxorqValidationException(typeof(TRequest).Name, failures);

        return await next().ConfigureAwait(false);
    }
}

