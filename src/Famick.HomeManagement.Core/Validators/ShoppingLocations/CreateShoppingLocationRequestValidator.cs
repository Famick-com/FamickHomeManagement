using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLocations;

public class CreateShoppingLocationRequestValidator : AbstractValidator<CreateShoppingLocationRequest>
{
    public CreateShoppingLocationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Shopping location name is required")
            .MaximumLength(200).WithMessage("Shopping location name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.StoreAddress)
            .MaximumLength(500).WithMessage("Store address cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.StoreAddress));

        RuleFor(x => x.StorePhone)
            .MaximumLength(50).WithMessage("Store phone cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.StorePhone));

        // Integration validation: if PluginId is provided, ExternalLocationId must also be provided
        RuleFor(x => x.ExternalLocationId)
            .NotEmpty().WithMessage("External location ID is required when creating from integration")
            .When(x => !string.IsNullOrEmpty(x.PluginId));
    }
}
