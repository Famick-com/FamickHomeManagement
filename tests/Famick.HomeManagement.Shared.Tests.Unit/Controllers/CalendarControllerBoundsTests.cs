using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Calendar;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Issue #120: the calendar endpoints refuse unbounded ranges and unsupported rules
/// before anything is expanded or stored.
/// </summary>
public class CalendarControllerBoundsTests
{
    private readonly Mock<ICalendarEventService> _events = new();
    private readonly Mock<ICalendarAvailabilityService> _availability = new();
    private readonly CalendarController _controller;

    public CalendarControllerBoundsTests()
    {
        _controller = new CalendarController(
            _events.Object,
            Mock.Of<IExternalCalendarService>(),
            _availability.Object,
            Mock.Of<IUserManagementService>(),
            Mock.Of<IReminderSyncPushService>(),
            new CalendarEventFilterRequestValidator(),
            new CreateCalendarEventRequestValidator(),
            new UpdateCalendarEventRequestValidator(),
            new FindSlotsRequestValidator(),
            Mock.Of<ITenantProvider>(t => t.TenantId == Guid.NewGuid() && t.UserId == Guid.NewGuid()),
            NullLogger<CalendarController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task A_thousand_year_range_is_refused()
    {
        var start = DateTime.UtcNow;
        var result = await _controller.GetEvents(new CalendarEventFilterRequest
        {
            StartDate = start, EndDate = start.AddYears(1000)
        });

        StatusOf(result).Should().Be(400);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task The_widest_client_range_is_served()
    {
        var start = DateTime.UtcNow;
        _events.Setup(e => e.GetCalendarEventsAsync(It.IsAny<CalendarEventFilterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetEvents(new CalendarEventFilterRequest
        {
            StartDate = start.AddDays(-30), EndDate = start.AddDays(90)
        });

        StatusOf(result).Should().Be(200);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(CalendarLimits.MaxUpcomingDays + 1)]
    [InlineData(int.MaxValue)]
    public async Task Upcoming_days_out_of_range_are_refused(int days)
    {
        StatusOf(await _controller.GetUpcomingEvents(days)).Should().Be(400);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_secondly_rule_is_refused_on_create()
    {
        var result = await _controller.CreateEvent(new CreateCalendarEventRequest
        {
            Title = "Every second", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "FREQ=SECONDLY"
        });

        StatusOf(result).Should().Be(400);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_fast_rule_is_refused_on_update()
    {
        var result = await _controller.UpdateEvent(Guid.NewGuid(), new UpdateCalendarEventRequest
        {
            Title = "Every minute", StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "FREQ=DAILY;BYMINUTE=0,1,2"
        });

        StatusOf(result).Should().Be(400);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_long_free_busy_range_is_refused()
    {
        var start = DateTime.UtcNow;
        var result = await _controller.GetFreeBusy([Guid.NewGuid()], start, start.AddDays(CalendarLimits.MaxAvailabilityRangeDays + 1));

        StatusOf(result).Should().Be(400);
        _availability.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_long_slot_search_is_refused()
    {
        var start = DateTime.UtcNow;
        var result = await _controller.FindAvailableSlots(new FindSlotsRequest
        {
            UserIds = [Guid.NewGuid()], DurationMinutes = 30, StartDate = start, EndDate = start.AddYears(10), MaxResults = 10
        });

        StatusOf(result).Should().Be(400);
        _availability.VerifyNoOtherCalls();
    }

    private static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };
}
