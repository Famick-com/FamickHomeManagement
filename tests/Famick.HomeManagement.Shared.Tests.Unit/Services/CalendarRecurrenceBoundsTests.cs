using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Calendar;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #120: a stored rule that is too fast or malformed is skipped instead of breaking
/// every read of the household's calendar, and expansion is capped per event and per response.
/// </summary>
public class CalendarRecurrenceBoundsTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();
    private readonly string _databaseName = $"calendar-bounds-{Guid.NewGuid()}";

    [Theory]
    [InlineData("FREQ=SECONDLY")]
    [InlineData("FREQ=DAILY;BYMINUTE=0,1,2")]
    [InlineData("this is not a rule")]
    // Passes the rule check, but there is no month 13 for Ical.Net to parse.
    [InlineData("FREQ=DAILY;UNTIL=20271340")]
    public async Task A_bad_stored_rule_does_not_break_the_other_events(string badRule)
    {
        var now = DateTime.UtcNow;
        Seed(
            Event("Single", now.AddDays(1)),
            Event("Weekly", now.AddDays(-14), "FREQ=WEEKLY"),
            Event("Bad", now.AddDays(-14), badRule));

        var occurrences = await EventService().GetCalendarEventsAsync(new CalendarEventFilterRequest
        {
            StartDate = now, EndDate = now.AddDays(30), IncludeExternalEvents = false
        });

        occurrences.Select(o => o.Title).Distinct().Should().BeEquivalentTo("Single", "Weekly");
        occurrences.Count(o => o.Title == "Weekly").Should().BeInRange(4, 5);
    }

    [Fact]
    public async Task One_event_expands_to_at_most_the_per_event_cap()
    {
        var start = new DateTime(2020, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        Seed(Event("Daily", start, "FREQ=DAILY"));

        // Past the controller's range limit on purpose: the service caps on its own.
        var occurrences = await EventService().GetCalendarEventsAsync(new CalendarEventFilterRequest
        {
            StartDate = start, EndDate = start.AddDays(3000), IncludeExternalEvents = false
        });

        occurrences.Should().HaveCount(CalendarLimits.MaxOccurrencesPerEvent);
        occurrences[^1].StartTimeUtc.Should().Be(start.AddDays(CalendarLimits.MaxOccurrencesPerEvent - 1));
    }

    [Fact]
    public async Task A_response_carries_at_most_the_per_response_cap_earliest_first()
    {
        var start = new DateTime(2020, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var events = Enumerable.Range(0, 11).Select(i => Event($"Daily {i}", start.AddMinutes(i), "FREQ=DAILY")).ToArray();
        Seed(events);

        var occurrences = await EventService().GetCalendarEventsAsync(new CalendarEventFilterRequest
        {
            StartDate = start, EndDate = start.AddDays(3000), IncludeExternalEvents = false
        });

        occurrences.Should().HaveCount(CalendarLimits.MaxOccurrencesPerResponse);
        occurrences.Should().BeInAscendingOrder(o => o.StartTimeUtc);
        occurrences[^1].StartTimeUtc.Should().BeBefore(start.AddDays(CalendarLimits.MaxOccurrencesPerEvent));
    }

    [Fact]
    public async Task A_bad_stored_rule_does_not_stop_the_reminder_sweep()
    {
        var now = DateTime.UtcNow;
        // A daily series whose occurrence today starts in half an hour, inside its hour reminder.
        var good = Event("Standup", now.AddMinutes(30).AddDays(-10), "FREQ=DAILY", reminderMinutes: 60);
        var bad = Event("Bad", now.AddMinutes(30).AddDays(-10), "FREQ=SECONDLY", reminderMinutes: 60);
        Seed(bad, good);

        await using var db = Context();
        var notifications = await new CalendarEventEvaluator(db, NullLogger<CalendarEventEvaluator>.Instance)
            .EvaluateAsync(Tenant);

        notifications.Should().ContainSingle().Which.Title.Should().Contain("Standup");
    }

    [Fact]
    public async Task A_bad_stored_rule_does_not_break_free_busy()
    {
        var now = DateTime.UtcNow;
        Seed(Event("Weekly", now.AddDays(-14), "FREQ=WEEKLY"), Event("Bad", now.AddDays(-14), "garbage"));

        await using var db = Context();
        var result = await new CalendarAvailabilityService(db, NullLogger<CalendarAvailabilityService>.Instance)
            .GetFreeBusyAsync([Member], now, now.AddDays(14));

        result.Should().ContainSingle().Which.BusySlots.Should().NotBeEmpty();
    }

    private CalendarEventService EventService() =>
        new(Context(), Mock.Of<IFileUrlService>(), NullLogger<CalendarEventService>.Instance);

    private HomeManagementDbContext Context()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(Tenant);
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new HomeManagementDbContext(options, tenantProvider.Object);
    }

    private void Seed(params CalendarEvent[] events)
    {
        using var db = Context();
        db.CalendarEvents.AddRange(events);
        db.SaveChanges();
    }

    private static CalendarEvent Event(string title, DateTime start, string? rule = null, int? reminderMinutes = null)
    {
        var id = Guid.NewGuid();
        return new CalendarEvent
        {
            Id = id,
            TenantId = Tenant,
            Title = title,
            StartTimeUtc = start,
            EndTimeUtc = start.AddHours(1),
            RecurrenceRule = rule,
            ReminderMinutesBefore = reminderMinutes,
            Members =
            [
                new CalendarEventMember
                {
                    Id = Guid.NewGuid(), CalendarEventId = id, UserId = Member,
                    ParticipationType = ParticipationType.Involved
                }
            ]
        };
    }
}
