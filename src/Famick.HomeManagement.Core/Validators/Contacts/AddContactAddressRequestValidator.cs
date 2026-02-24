using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class AddContactAddressRequestValidator : AbstractValidator<AddContactAddressRequest>
{
    public AddContactAddressRequestValidator()
    {
        RuleFor(x => x.Tag)
            .IsInEnum().WithMessage("Invalid address tag");

        // When not using existing address, require at least city or address line 1
        When(x => !x.AddressId.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.AddressLine1) || !string.IsNullOrWhiteSpace(x.City))
                .WithMessage("Either address line 1 or city is required for a new address");
        });

        RuleFor(x => x.AddressLine1)
            .MaximumLength(200).WithMessage("Address line 1 cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.AddressLine1));

        RuleFor(x => x.AddressLine2)
            .MaximumLength(200).WithMessage("Address line 2 cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.AddressLine2));

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.StateProvince)
            .MaximumLength(100).WithMessage("State/Province cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.StateProvince));

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Country));

        RuleFor(x => x.CountryCode)
            .MaximumLength(5).WithMessage("Country code cannot exceed 5 characters")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}
