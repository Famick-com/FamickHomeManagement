using Famick.HomeManagement.Core.DTOs.Tenant;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Tenant;

public class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Household name is required")
            .MaximumLength(255).WithMessage("Household name cannot exceed 255 characters");

        When(x => x.Address != null, () =>
        {
            RuleFor(x => x.Address!.AddressLine1)
                .MaximumLength(255).WithMessage("Address line 1 cannot exceed 255 characters");

            RuleFor(x => x.Address!.AddressLine2)
                .MaximumLength(255).WithMessage("Address line 2 cannot exceed 255 characters");

            RuleFor(x => x.Address!.AddressLine3)
                .MaximumLength(255).WithMessage("Address line 3 cannot exceed 255 characters");

            RuleFor(x => x.Address!.AddressLine4)
                .MaximumLength(255).WithMessage("Address line 4 cannot exceed 255 characters");

            RuleFor(x => x.Address!.City)
                .MaximumLength(100).WithMessage("City cannot exceed 100 characters");

            RuleFor(x => x.Address!.StateProvince)
                .MaximumLength(100).WithMessage("State/Province cannot exceed 100 characters");

            RuleFor(x => x.Address!.PostalCode)
                .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");

            RuleFor(x => x.Address!.Country)
                .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");

            RuleFor(x => x.Address!.CountryCode)
                .MaximumLength(2).WithMessage("Country code must be 2 characters (ISO 3166-1 alpha-2)");
        });
    }
}
