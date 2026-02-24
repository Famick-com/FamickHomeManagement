using Famick.HomeManagement.Core.DTOs.Home;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Home;

public class CreateHomeUtilityRequestValidator : AbstractValidator<CreateHomeUtilityRequest>
{
    public CreateHomeUtilityRequestValidator()
    {
        RuleFor(x => x.UtilityType)
            .IsInEnum().WithMessage("Invalid utility type");

        RuleFor(x => x.CompanyName)
            .MaximumLength(255).WithMessage("Company name cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.CompanyName));

        RuleFor(x => x.AccountNumber)
            .MaximumLength(100).WithMessage("Account number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.AccountNumber));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Website)
            .MaximumLength(500).WithMessage("Website cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Website));

        RuleFor(x => x.LoginEmail)
            .MaximumLength(255).WithMessage("Login email cannot exceed 255 characters")
            .EmailAddress().WithMessage("Login email must be a valid email address")
            .When(x => !string.IsNullOrEmpty(x.LoginEmail));
    }
}
