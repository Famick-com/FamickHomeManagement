using Famick.HomeManagement.Core.DTOs.ProductGroups;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ProductGroups;

public class UpdateProductGroupRequestValidator : AbstractValidator<UpdateProductGroupRequest>
{
    public UpdateProductGroupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product group name is required")
            .MaximumLength(200).WithMessage("Product group name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
