using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLocations;

public class UpdateShoppingLocationRequestValidator : AbstractValidator<UpdateShoppingLocationRequest>
{
    public UpdateShoppingLocationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Shopping location name is required")
            .MaximumLength(200).WithMessage("Shopping location name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
