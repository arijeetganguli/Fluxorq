namespace Fluxorq.Abstractions.Exceptions;

/// <summary>
/// Thrown by <see cref="Pipeline.ValidationBehavior{TRequest,TResponse}"/> when one or more validators
/// report failures for the dispatched request.
/// </summary>
public sealed class FluxorqValidationException : Exception
{
    /// <summary>The name of the request type that failed validation.</summary>
    public string RequestName { get; }

    /// <summary>All validation failures gathered across every registered validator.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public FluxorqValidationException(string requestName, IReadOnlyList<ValidationFailure> failures)
        : base(BuildMessage(requestName, failures))
    {
        RequestName = requestName;
        Failures = failures;
    }

    private static string BuildMessage(string requestName, IReadOnlyList<ValidationFailure> failures)
    {
        var details = string.Join("; ", failures.Select(f => f.ToString()));
        return $"Validation failed for '{requestName}': {details}";
    }
}
