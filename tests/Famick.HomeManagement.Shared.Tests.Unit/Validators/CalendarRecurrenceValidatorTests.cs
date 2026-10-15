using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Validators.Calendar;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

/// <summary>
/// Issue #120: only daily-or-slower rules that pick days are stored, and calendar reads
/// are bounded. The allowed rules are the ones the web and mobile clients write today.
/// </summary>
public class CalendarRecurrenceValidatorTests
{
    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=DAILY;INTERVAL=1")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2")]
    [InlineData("FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,FR")]
    [InlineData("FREQ=MONTHLY;BYDAY=-1FR")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,15,-1")]
    [InlineData("FREQ=YEARLY;BYMONTH=3;BYDAY=2SU")]
    [InlineData("FREQ=MONTHLY;UNTIL=20271231T000000Z")]
    [InlineData("FREQ=YEARLY;COUNT=5")]
    [InlineData("freq=weekly;wkst=su")]
    public void Client_rules_are_allowed(string rule) =>
        RecurrenceRuleCheck.Problem(rule).Should().BeNull();

    [Theory]
    [InlineData("FREQ=SECONDLY")]
    [InlineData("FREQ=MINUTELY")]
    [InlineData("FREQ=HOURLY")]
    [InlineData("FREQ=DAILY;BYHOUR=0,1,2,3")]
    [InlineData("FREQ=DAILY;BYMINUTE=0,1,2")]
    [InlineData("FREQ=DAILY;BYSECOND=0")]
    [InlineData("FREQ=DAILY;INTERVAL=0")]
    [InlineData("FREQ=DAILY;COUNT=2;UNTIL=20270101")]
    [InlineData("FREQ=DAILY;FREQ=WEEKLY")]
    [InlineData("FREQ=WEEKLY;BYDAY=XX")]
    [InlineData("INTERVAL=2")]
    [InlineData("not a rule")]
    public void Fast_or_malformed_rules_are_refused(string rule) =>
        RecurrenceRuleCheck.Problem(rule).Should().NotBeNull();

    [Fact]
    public void A_secondly_rule_is_refused_on_create()
    {
        var result = new CreateCalendarEventRequestValidator().Validate(Create("FREQ=SECONDLY"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "RecurrenceRule")
            .Which.ErrorMessage.Should().Contain("FREQ");
    }

    [Fact]
    public void A_daily_rule_that_fires_every_minute_is_refused_on_update()
    {
        var request = new UpdateCalendarEventRequest
        {
            Title = "t", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "FREQ=DAILY;BYHOUR=0,1,2;BYMINUTE=0,1,2", EditScope = RecurrenceEditScope.EntireSeries
        };

        new UpdateCalendarEventRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_create_without_members_is_allowed_because_the_service_adds_the_creator() =>
        new CreateCalendarEventRequestValidator().Validate(Create("FREQ=WEEKLY")).IsValid.Should().BeTrue();

    [Fact]
    public void Making_a_single_event_repeat_needs_no_edit_scope()
    {
        // The web client only asks for a scope when the stored event already repeats.
        var request = new UpdateCalendarEventRequest
        {
            Title = "t", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "FREQ=WEEKLY"
        };

        new UpdateCalendarEventRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(CalendarLimits.MaxEventRangeDays, true)]
    [InlineData(CalendarLimits.MaxEventRangeDays + 1, false)]
    [InlineData(365 * 1000, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Event_ranges_are_bounded(int days, bool valid)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new CalendarEventFilterRequest { StartDate = start, EndDate = start.AddDays(days) };

        new CalendarEventFilterRequestValidator().Validate(filter).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Slot_searches_are_limited_in_users()
    {
        var request = new FindSlotsRequest
        {
            UserIds = Enumerable.Range(0, CalendarLimits.MaxAvailabilityUsers + 1).Select(_ => Guid.NewGuid()).ToList(),
            DurationMinutes = 30, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7), MaxResults = 10
        };

        new FindSlotsRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    private static CreateCalendarEventRequest Create(string rule) => new()
    {
        Title = "Event",
        StartTimeUtc = DateTime.UtcNow,
        EndTimeUtc = DateTime.UtcNow.AddHours(1),
        RecurrenceRule = rule
    };
}
