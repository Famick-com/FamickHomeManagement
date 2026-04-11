using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class CalendarMappingTests
{

    #region CalendarEvent -> CalendarEventDto

    [Fact]
    public void CalendarEvent_To_CalendarEventDto_MapsAllProperties()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var calendarEvent = new CalendarEvent
        {
            Id = eventId,
            TenantId = Guid.NewGuid(),
            Title = "Team Meeting",
            Description = "Weekly sync",
            Location = "Conference Room A",
            StartTimeUtc = now,
            EndTimeUtc = now.AddHours(1),
            IsAllDay = false,
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
            RecurrenceEndDate = now.AddMonths(6),
            ReminderMinutesBefore = 15,
            Color = "#FF5733",
            CreatedByUserId = userId,
            CreatedByUser = new User { FirstName = "John", LastName = "Doe" },
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now
        };

        var dto = CalendarMapper.ToDto(calendarEvent);

        dto.Id.Should().Be(eventId);
        dto.Title.Should().Be("Team Meeting");
        dto.Description.Should().Be("Weekly sync");
        dto.Location.Should().Be("Conference Room A");
        dto.StartTimeUtc.Should().Be(now);
        dto.EndTimeUtc.Should().Be(now.AddHours(1));
        dto.IsAllDay.Should().BeFalse();
        dto.RecurrenceRule.Should().Be("FREQ=WEEKLY;BYDAY=MO");
        dto.RecurrenceEndDate.Should().Be(now.AddMonths(6));
        dto.ReminderMinutesBefore.Should().Be(15);
        dto.Color.Should().Be("#FF5733");
        dto.CreatedByUserId.Should().Be(userId);
        dto.CreatedByUserName.Should().Be("John Doe");
        dto.CreatedAt.Should().Be(now.AddDays(-1));
        dto.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void CalendarEvent_To_CalendarEventDto_NullCreatedByUser_ReturnsNullUserName()
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Solo Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            CreatedByUser = null
        };

        var dto = CalendarMapper.ToDto(calendarEvent);

        dto.CreatedByUserName.Should().BeNull();
    }

    [Fact]
    public void CalendarEvent_To_CalendarEventDto_UserWithOnlyFirstName_TrimsResult()
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            CreatedByUser = new User { FirstName = "Alice", LastName = "" }
        };

        var dto = CalendarMapper.ToDto(calendarEvent);

        dto.CreatedByUserName.Should().Be("Alice");
    }

    #endregion

    #region CalendarEvent -> CalendarEventSummaryDto

    [Fact]
    public void CalendarEvent_To_CalendarEventSummaryDto_MapsAllProperties()
    {
        var now = DateTime.UtcNow;
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Daily Standup",
            StartTimeUtc = now,
            EndTimeUtc = now.AddMinutes(15),
            IsAllDay = false,
            Color = "#00FF00",
            RecurrenceRule = "FREQ=DAILY",
            Members = new List<CalendarEventMember>
            {
                new() { UserId = Guid.NewGuid() },
                new() { UserId = Guid.NewGuid() },
                new() { UserId = Guid.NewGuid() }
            }
        };

        var dto = CalendarMapper.ToSummaryDto(calendarEvent);

        dto.Id.Should().Be(calendarEvent.Id);
        dto.Title.Should().Be("Daily Standup");
        dto.StartTimeUtc.Should().Be(now);
        dto.EndTimeUtc.Should().Be(now.AddMinutes(15));
        dto.IsAllDay.Should().BeFalse();
        dto.Color.Should().Be("#00FF00");
        dto.IsRecurring.Should().BeTrue();
        dto.MemberCount.Should().Be(3);
    }

    [Fact]
    public void CalendarEvent_To_CalendarEventSummaryDto_NoRecurrenceRule_IsRecurringFalse()
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "One-Off Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = null,
            Members = new List<CalendarEventMember>()
        };

        var dto = CalendarMapper.ToSummaryDto(calendarEvent);

        dto.IsRecurring.Should().BeFalse();
        dto.MemberCount.Should().Be(0);
    }

    [Fact]
    public void CalendarEvent_To_CalendarEventSummaryDto_EmptyRecurrenceRule_IsRecurringFalse()
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "",
            Members = new List<CalendarEventMember>()
        };

        var dto = CalendarMapper.ToSummaryDto(calendarEvent);

        dto.IsRecurring.Should().BeFalse();
    }

    #endregion

    #region CalendarEventMember -> CalendarEventMemberDto

    [Fact]
    public void CalendarEventMember_To_CalendarEventMemberDto_MapsAllProperties()
    {
        var userId = Guid.NewGuid();
        var member = new CalendarEventMember
        {
            Id = Guid.NewGuid(),
            CalendarEventId = Guid.NewGuid(),
            UserId = userId,
            ParticipationType = ParticipationType.Aware,
            User = new User { FirstName = "Jane", LastName = "Smith" }
        };

        var dto = CalendarMapper.ToMemberDto(member);

        dto.UserId.Should().Be(userId);
        dto.ParticipationType.Should().Be(ParticipationType.Aware);
        dto.UserDisplayName.Should().Be("Jane Smith");
    }

    [Fact]
    public void CalendarEventMember_To_CalendarEventMemberDto_NullUser_ReturnsEmptyDisplayName()
    {
        var member = new CalendarEventMember
        {
            UserId = Guid.NewGuid(),
            ParticipationType = ParticipationType.Involved,
            User = null
        };

        var dto = CalendarMapper.ToMemberDto(member);

        dto.UserDisplayName.Should().Be(string.Empty);
    }

    #endregion

    #region CalendarEventException -> CalendarEventExceptionDto

    [Fact]
    public void CalendarEventException_To_CalendarEventExceptionDto_MapsAllProperties()
    {
        var now = DateTime.UtcNow;
        var exception = new CalendarEventException
        {
            Id = Guid.NewGuid(),
            CalendarEventId = Guid.NewGuid(),
            OriginalStartTimeUtc = now,
            IsDeleted = false,
            OverrideTitle = "Rescheduled Meeting",
            OverrideDescription = "Moved to afternoon",
            OverrideLocation = "Room B",
            OverrideStartTimeUtc = now.AddHours(4),
            OverrideEndTimeUtc = now.AddHours(5),
            OverrideIsAllDay = true
        };

        var dto = CalendarMapper.ToExceptionDto(exception);

        dto.Id.Should().Be(exception.Id);
        dto.OriginalStartTimeUtc.Should().Be(now);
        dto.IsDeleted.Should().BeFalse();
        dto.OverrideTitle.Should().Be("Rescheduled Meeting");
        dto.OverrideDescription.Should().Be("Moved to afternoon");
        dto.OverrideLocation.Should().Be("Room B");
        dto.OverrideStartTimeUtc.Should().Be(now.AddHours(4));
        dto.OverrideEndTimeUtc.Should().Be(now.AddHours(5));
        dto.OverrideIsAllDay.Should().BeTrue();
    }

    [Fact]
    public void CalendarEventException_To_CalendarEventExceptionDto_DeletedOccurrence()
    {
        var exception = new CalendarEventException
        {
            Id = Guid.NewGuid(),
            OriginalStartTimeUtc = DateTime.UtcNow,
            IsDeleted = true,
            OverrideTitle = null,
            OverrideStartTimeUtc = null,
            OverrideEndTimeUtc = null,
            OverrideIsAllDay = null
        };

        var dto = CalendarMapper.ToExceptionDto(exception);

        dto.IsDeleted.Should().BeTrue();
        dto.OverrideTitle.Should().BeNull();
        dto.OverrideStartTimeUtc.Should().BeNull();
        dto.OverrideEndTimeUtc.Should().BeNull();
        dto.OverrideIsAllDay.Should().BeNull();
    }

    #endregion

    #region CreateCalendarEventRequest -> CalendarEvent

    [Fact]
    public void CreateCalendarEventRequest_To_CalendarEvent_MapsEditableFields()
    {
        var now = DateTime.UtcNow;
        var request = new CreateCalendarEventRequest
        {
            Title = "New Event",
            Description = "Description",
            Location = "Office",
            StartTimeUtc = now,
            EndTimeUtc = now.AddHours(2),
            IsAllDay = false,
            RecurrenceRule = "FREQ=MONTHLY",
            RecurrenceEndDate = now.AddYears(1),
            ReminderMinutesBefore = 30,
            Color = "#ABCDEF"
        };

        var entity = CalendarMapper.FromCreateRequest(request);

        entity.Title.Should().Be("New Event");
        entity.Description.Should().Be("Description");
        entity.Location.Should().Be("Office");
        entity.StartTimeUtc.Should().Be(now);
        entity.EndTimeUtc.Should().Be(now.AddHours(2));
        entity.IsAllDay.Should().BeFalse();
        entity.RecurrenceRule.Should().Be("FREQ=MONTHLY");
        entity.RecurrenceEndDate.Should().Be(now.AddYears(1));
        entity.ReminderMinutesBefore.Should().Be(30);
        entity.Color.Should().Be("#ABCDEF");
    }

    [Fact]
    public void CreateCalendarEventRequest_To_CalendarEvent_IgnoresSystemFields()
    {
        var request = new CreateCalendarEventRequest
        {
            Title = "Test",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1)
        };

        var entity = CalendarMapper.FromCreateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedByUserId.Should().Be(Guid.Empty);
        entity.CreatedByUser.Should().BeNull();
        entity.Members.Should().BeEmpty();
        entity.Exceptions.Should().BeEmpty();
    }

    #endregion

    #region UpdateCalendarEventRequest -> CalendarEvent

    [Fact]
    public void UpdateCalendarEventRequest_To_CalendarEvent_MapsEditableFields()
    {
        var now = DateTime.UtcNow;
        var request = new UpdateCalendarEventRequest
        {
            Title = "Updated Event",
            Description = "Updated desc",
            Location = "New Location",
            StartTimeUtc = now,
            EndTimeUtc = now.AddHours(3),
            IsAllDay = true,
            RecurrenceRule = "FREQ=WEEKLY",
            RecurrenceEndDate = now.AddMonths(3),
            ReminderMinutesBefore = 10,
            Color = "#112233",
            EditScope = RecurrenceEditScope.ThisOccurrence,
            OccurrenceStartTimeUtc = now.AddDays(-7)
        };

        var entity = CalendarMapper.FromUpdateRequest(request);

        entity.Title.Should().Be("Updated Event");
        entity.Description.Should().Be("Updated desc");
        entity.Location.Should().Be("New Location");
        entity.StartTimeUtc.Should().Be(now);
        entity.EndTimeUtc.Should().Be(now.AddHours(3));
        entity.IsAllDay.Should().BeTrue();
        entity.RecurrenceRule.Should().Be("FREQ=WEEKLY");
        entity.RecurrenceEndDate.Should().Be(now.AddMonths(3));
        entity.ReminderMinutesBefore.Should().Be(10);
        entity.Color.Should().Be("#112233");
    }

    [Fact]
    public void UpdateCalendarEventRequest_To_CalendarEvent_IgnoresSystemFields()
    {
        var request = new UpdateCalendarEventRequest
        {
            Title = "Test",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1)
        };

        var entity = CalendarMapper.FromUpdateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedByUserId.Should().Be(Guid.Empty);
        entity.CreatedByUser.Should().BeNull();
        entity.Members.Should().BeEmpty();
        entity.Exceptions.Should().BeEmpty();
    }

    #endregion

    #region CalendarEventMemberRequest -> CalendarEventMember

    [Fact]
    public void CalendarEventMemberRequest_To_CalendarEventMember_MapsEditableFields()
    {
        var userId = Guid.NewGuid();
        var request = new CalendarEventMemberRequest
        {
            UserId = userId,
            ParticipationType = ParticipationType.Aware
        };

        var entity = CalendarMapper.FromMemberRequest(request);

        entity.UserId.Should().Be(userId);
        entity.ParticipationType.Should().Be(ParticipationType.Aware);
    }

    [Fact]
    public void CalendarEventMemberRequest_To_CalendarEventMember_IgnoresSystemFields()
    {
        var request = new CalendarEventMemberRequest
        {
            UserId = Guid.NewGuid(),
            ParticipationType = ParticipationType.Involved
        };

        var entity = CalendarMapper.FromMemberRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.CalendarEventId.Should().Be(Guid.Empty);
        entity.CalendarEvent.Should().BeNull();
        entity.User.Should().BeNull();
    }

    #endregion

    #region ExternalCalendarEvent -> CalendarOccurrenceDto

    [Fact]
    public void ExternalCalendarEvent_To_CalendarOccurrenceDto_MapsAllProperties()
    {
        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var externalEvent = new ExternalCalendarEvent
        {
            Id = eventId,
            Title = "External Meeting",
            StartTimeUtc = now,
            EndTimeUtc = now.AddHours(1),
            IsAllDay = false,
            Subscription = new ExternalCalendarSubscription
            {
                Color = "#FF0000",
                User = new User { FirstName = "Bob", LastName = "Jones" }
            }
        };

        var dto = CalendarMapper.ToOccurrenceDto(externalEvent);

        dto.EventId.Should().Be(eventId);
        dto.Title.Should().Be("External Meeting");
        dto.StartTimeUtc.Should().Be(now);
        dto.EndTimeUtc.Should().Be(now.AddHours(1));
        dto.IsAllDay.Should().BeFalse();
        dto.Color.Should().Be("#FF0000");
        dto.IsExternal.Should().BeTrue();
        dto.OwnerDisplayName.Should().Be("Bob Jones");
    }

    [Fact]
    public void ExternalCalendarEvent_To_CalendarOccurrenceDto_NullSubscription_ReturnsNullColorAndOwner()
    {
        var externalEvent = new ExternalCalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Orphan Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            Subscription = null
        };

        var dto = CalendarMapper.ToOccurrenceDto(externalEvent);

        dto.Color.Should().BeNull();
        dto.OwnerDisplayName.Should().BeNull();
        dto.IsExternal.Should().BeTrue();
    }

    [Fact]
    public void ExternalCalendarEvent_To_CalendarOccurrenceDto_SubscriptionWithNullUser_ReturnsNullOwner()
    {
        var externalEvent = new ExternalCalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Event",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            Subscription = new ExternalCalendarSubscription
            {
                Color = "#00FF00",
                User = null
            }
        };

        var dto = CalendarMapper.ToOccurrenceDto(externalEvent);

        dto.Color.Should().Be("#00FF00");
        dto.OwnerDisplayName.Should().BeNull();
    }

    [Fact]
    public void ExternalCalendarEvent_To_CalendarOccurrenceDto_IgnoredFieldsAreDefault()
    {
        var externalEvent = new ExternalCalendarEvent
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            Subscription = null
        };

        var dto = CalendarMapper.ToOccurrenceDto(externalEvent);

        dto.Description.Should().BeNull();
        dto.Location.Should().BeNull();
        dto.OriginalStartTimeUtc.Should().BeNull();
        dto.Members.Should().BeEmpty();
        dto.OwnerProfileImageUrl.Should().BeNull();
    }

    #endregion
}
