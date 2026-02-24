using Famick.HomeManagement.Core.DTOs.Notifications;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Notifications;

public class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Device token is required")
            .MaximumLength(500).WithMessage("Device token cannot exceed 500 characters");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Invalid device platform");
    }
}
