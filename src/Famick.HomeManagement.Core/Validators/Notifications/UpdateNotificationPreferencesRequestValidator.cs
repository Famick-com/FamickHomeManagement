using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Domain.Enums;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Notifications;

public class UpdateNotificationPreferencesRequestValidator : AbstractValidator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesRequestValidator()
    {
        RuleFor(x => x.Preferences)
            .NotNull().WithMessage("Preferences list is required")
            .NotEmpty().WithMessage("At least one preference must be provided");

        RuleForEach(x => x.Preferences).ChildRules(pref =>
        {
            pref.RuleFor(p => p.MessageType)
                .IsInEnum().WithMessage("Invalid notification type")
                .Must(t => t.IsNotification())
                .WithMessage("Cannot set preferences for transactional message types");
        });
    }
}
