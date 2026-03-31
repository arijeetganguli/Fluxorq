using Fluxorq.Abstractions;
using Fluxorq.Sample.Models;

namespace Fluxorq.Sample.Validators;

public class CreateProductRequestValidator : IValidator<CreateProductRequest>
{
    public ValidationResult Validate(CreateProductRequest instance)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(instance.Name))
            failures.Add(new ValidationFailure(nameof(instance.Name), "Product name is required."));

        if (instance.Price <= 0)
            failures.Add(new ValidationFailure(nameof(instance.Price), "Price must be greater than zero."));

        if (instance.InitialStock < 0)
            failures.Add(new ValidationFailure(nameof(instance.InitialStock), "Initial stock cannot be negative."));

        return failures.Count > 0
            ? ValidationResult.Fail(failures)
            : ValidationResult.Success;
    }
}
