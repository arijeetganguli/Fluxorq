namespace Fluxorq.Abstractions;

/// <summary>The outcome of running one or more <see cref="IValidator{T}"/> instances.</summary>
public sealed class ValidationResult
{
    private static readonly ValidationResult _success = new(true, []);

    /// <summary>A pre-allocated successful result with no failures.</summary>
    public static ValidationResult Success => _success;

    /// <summary>Creates a failed result from the supplied failures.</summary>
    public static ValidationResult Fail(IReadOnlyList<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
            throw new ArgumentException("A failed ValidationResult must contain at least one failure.", nameof(failures));

        return new ValidationResult(false, failures);
    }

    /// <summary>Creates a failed result from a single failure.</summary>
    public static ValidationResult Fail(string propertyName, string errorMessage)
        => Fail([new ValidationFailure(propertyName, errorMessage)]);

    /// <summary><c>true</c> if validation passed; <c>false</c> if any failures were recorded.</summary>
    public bool IsValid { get; }

    /// <summary>The list of failures. Empty when <see cref="IsValid"/> is <c>true</c>.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    private ValidationResult(bool isValid, IReadOnlyList<ValidationFailure> failures)
    {
        IsValid = isValid;
        Failures = failures;
    }
}
