using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class AddEmailRequestValidator : AbstractValidator<AddEmailRequest>
{
    public AddEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email address is not valid")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters");

        RuleFor(x => x.Tag)
            .IsInEnum().WithMessage("Invalid email tag");

        RuleFor(x => x.Label)
            .MaximumLength(100).WithMessage("Label cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Label));
    }
}
