using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class CreateContactTagRequestValidator : AbstractValidator<CreateContactTagRequest>
{
    public CreateContactTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required")
            .MaximumLength(100).WithMessage("Tag name cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("Color cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Icon));
    }
}
