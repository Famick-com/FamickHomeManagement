using Famick.HomeManagement.Core.DTOs.Products;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators;

public class CreateProductFromLookupRequestValidator : AbstractValidator<CreateProductFromLookupRequest>
{
    public CreateProductFromLookupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters");
    }
}
