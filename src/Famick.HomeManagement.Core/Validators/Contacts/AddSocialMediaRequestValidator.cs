using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class AddSocialMediaRequestValidator : AbstractValidator<AddSocialMediaRequest>
{
    public AddSocialMediaRequestValidator()
    {
        RuleFor(x => x.Service)
            .IsInEnum().WithMessage("Invalid social media service");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MaximumLength(100).WithMessage("Username cannot exceed 100 characters");

        RuleFor(x => x.ProfileUrl)
            .MaximumLength(500).WithMessage("Profile URL cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.ProfileUrl));
    }
}
