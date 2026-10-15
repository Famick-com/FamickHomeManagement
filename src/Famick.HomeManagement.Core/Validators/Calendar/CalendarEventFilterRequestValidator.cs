using Famick.HomeManagement.Core.DTOs.Calendar;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Calendar;

public class CalendarEventFilterRequestValidator : AbstractValidator<CalendarEventFilterRequest>
{
    public CalendarEventFilterRequestValidator()
    {
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(x => x)
            .Must(x => (x.EndDate - x.StartDate).TotalDays <= CalendarLimits.MaxEventRangeDays)
            .WithName("EndDate")
            .WithMessage($"Date range cannot exceed {CalendarLimits.MaxEventRangeDays} days");
    }
}
