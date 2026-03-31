using Fluxorq.Abstractions;
using Fluxorq.Tests.Helpers;

namespace Fluxorq.Tests.Helpers;

// ─── Simple validators ────────────────────────────────────────────────────────

public class PingRequestValidator : IValidator<PingRequest>
{
    public ValidationResult Validate(PingRequest instance)
    {
        if (string.IsNullOrWhiteSpace(instance.Message))
            return ValidationResult.Fail(nameof(instance.Message), "Message must not be empty.");

        return ValidationResult.Success;
    }
}

public class AddRequestValidator : IValidator<AddRequest>
{
    public ValidationResult Validate(AddRequest instance)
    {
        var failures = new List<ValidationFailure>();

        if (instance.A < 0)
            failures.Add(new ValidationFailure(nameof(instance.A), "A must be non-negative."));

        if (instance.B < 0)
            failures.Add(new ValidationFailure(nameof(instance.B), "B must be non-negative."));

        return failures.Count > 0
            ? ValidationResult.Fail(failures)
            : ValidationResult.Success;
    }
}

// ─── Always-failing validator (for short-circuit tests) ──────────────────────

public class AlwaysInvalidValidator<T> : IValidator<T> where T : IRequest<object>
{
    public ValidationResult Validate(T instance)
        => ValidationResult.Fail("_global", "Always invalid.");
}
