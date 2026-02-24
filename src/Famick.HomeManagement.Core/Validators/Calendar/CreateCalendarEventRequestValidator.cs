using Famick.HomeManagement.Core.DTOs.Calendar;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Calendar;

public class CreateCalendarEventRequestValidator : AbstractValidator<CreateCalendarEventRequest>
{
    public CreateCalendarEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Event title is required")
            .MaximumLength(255).WithMessage("Event title cannot exceed 255 characters");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Location)
            .MaximumLength(500).WithMessage("Location cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.EndTimeUtc)
            .GreaterThan(x => x.StartTimeUtc)
            .WithMessage("End time must be after start time");

        RuleFor(x => x.RecurrenceRule)
            .MaximumLength(500).WithMessage("Recurrence rule cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.RecurrenceRule));

        RuleFor(x => x.RecurrenceEndDate)
            .GreaterThan(x => x.StartTimeUtc)
            .WithMessage("Recurrence end date must be after start time")
            .When(x => x.RecurrenceEndDate.HasValue);

        RuleFor(x => x.ReminderMinutesBefore)
            .GreaterThan(0).WithMessage("Reminder minutes must be greater than 0")
            .LessThanOrEqualTo(10080).WithMessage("Reminder cannot be more than 7 days (10080 minutes) before the event")
            .When(x => x.ReminderMinutesBefore.HasValue);

        RuleFor(x => x.Color)
            .MaximumLength(50).WithMessage("Color cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.Members)
            .NotEmpty().WithMessage("At least one member must be added to the event");

        RuleForEach(x => x.Members).ChildRules(member =>
        {
            member.RuleFor(m => m.UserId)
                .NotEmpty().WithMessage("Member user ID is required");

            member.RuleFor(m => m.ParticipationType)
                .IsInEnum().WithMessage("Invalid participation type");
        });
    }
}
