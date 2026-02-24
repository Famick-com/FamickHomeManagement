using Famick.HomeManagement.Core.DTOs.Calendar;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Calendar;

public class FindSlotsRequestValidator : AbstractValidator<FindSlotsRequest>
{
    public FindSlotsRequestValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("At least one user ID is required");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes")
            .LessThanOrEqualTo(1440).WithMessage("Duration cannot exceed 24 hours (1440 minutes)");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(x => x)
            .Must(x => (x.EndDate - x.StartDate).TotalDays <= 30)
            .WithMessage("Search range cannot exceed 30 days");

        RuleFor(x => x.PreferredStartHour)
            .InclusiveBetween(0, 23).WithMessage("Preferred start hour must be between 0 and 23")
            .When(x => x.PreferredStartHour.HasValue);

        RuleFor(x => x.PreferredEndHour)
            .InclusiveBetween(0, 23).WithMessage("Preferred end hour must be between 0 and 23")
            .When(x => x.PreferredEndHour.HasValue);

        RuleFor(x => x.MaxResults)
            .GreaterThan(0).WithMessage("Max results must be greater than 0")
            .LessThanOrEqualTo(50).WithMessage("Max results cannot exceed 50");
    }
}
