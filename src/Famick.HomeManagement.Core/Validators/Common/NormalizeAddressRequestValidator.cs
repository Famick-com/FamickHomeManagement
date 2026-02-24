using Famick.HomeManagement.Core.DTOs.Common;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Common;

public class NormalizeAddressRequestValidator : AbstractValidator<NormalizeAddressRequest>
{
    public NormalizeAddressRequestValidator()
    {
        // At least one field must be provided
        RuleFor(x => x)
            .Must(x =>
                !string.IsNullOrWhiteSpace(x.AddressLine1) ||
                !string.IsNullOrWhiteSpace(x.City) ||
                !string.IsNullOrWhiteSpace(x.PostalCode))
            .WithMessage("At least an address line, city, or postal code must be provided");

        RuleFor(x => x.AddressLine1)
            .MaximumLength(255).WithMessage("Address line 1 cannot exceed 255 characters");

        RuleFor(x => x.AddressLine2)
            .MaximumLength(255).WithMessage("Address line 2 cannot exceed 255 characters");

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters");

        RuleFor(x => x.StateProvince)
            .MaximumLength(100).WithMessage("State/Province cannot exceed 100 characters");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");
    }
}
