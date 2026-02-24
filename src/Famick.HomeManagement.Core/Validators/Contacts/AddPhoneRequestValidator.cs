using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class AddPhoneRequestValidator : AbstractValidator<AddPhoneRequest>
{
    public AddPhoneRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters");

        RuleFor(x => x.Tag)
            .IsInEnum().WithMessage("Invalid phone tag");
    }
}
