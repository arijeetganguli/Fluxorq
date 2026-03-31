namespace Fluxorq.Abstractions;

/// <summary>
/// Validates an instance of <typeparamref name="T"/>.
/// Register implementations in DI; they are automatically picked up by <see cref="Pipeline.ValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Validates the given instance and returns a <see cref="ValidationResult"/>.</summary>
    ValidationResult Validate(T instance);
}
