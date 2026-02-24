using Famick.HomeManagement.Core.DTOs.Calendar;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Calendar;

public class CreateExternalCalendarSubscriptionRequestValidator : AbstractValidator<CreateExternalCalendarSubscriptionRequest>
{
    public CreateExternalCalendarSubscriptionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Subscription name is required")
            .MaximumLength(255).WithMessage("Subscription name cannot exceed 255 characters");

        RuleFor(x => x.IcsUrl)
            .NotEmpty().WithMessage("ICS URL is required")
            .MaximumLength(2048).WithMessage("ICS URL cannot exceed 2048 characters")
            .Must(BeAValidUrl).WithMessage("ICS URL must be a valid URL");

        RuleFor(x => x.Color)
            .MaximumLength(50).WithMessage("Color cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.SyncIntervalMinutes)
            .GreaterThanOrEqualTo(15).WithMessage("Sync interval must be at least 15 minutes")
            .LessThanOrEqualTo(1440).WithMessage("Sync interval cannot exceed 24 hours (1440 minutes)");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps
                   || result.Scheme == "webcal");
    }
}
