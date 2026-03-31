namespace Fluxorq.Abstractions;

/// <summary>Identifies a single validation failure for a property or the entire object.</summary>
public sealed class ValidationFailure
{
    /// <summary>The property or field that failed validation. Empty string indicates an object-level failure.</summary>
    public string PropertyName { get; }

    /// <summary>A human-readable description of why validation failed.</summary>
    public string ErrorMessage { get; }

    /// <param name="propertyName">Property or field name. Use <see cref="string.Empty"/> for object-level failures.</param>
    /// <param name="errorMessage">Description of the failure.</param>
    public ValidationFailure(string propertyName, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName, nameof(propertyName));
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage, nameof(errorMessage));

        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }

    public override string ToString() => $"{PropertyName}: {ErrorMessage}";
}
