#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class CalendarMapper
{
    // CalendarEvent -> CalendarEventDto (computed: CreatedByUserName)
    public static CalendarEventDto ToDto(CalendarEvent source)
    {
        var dto = MapCalendarEventToDto(source);
        dto.CreatedByUserName = source.CreatedByUser != null
            ? $"{source.CreatedByUser.FirstName} {source.CreatedByUser.LastName}".Trim()
            : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(CalendarEventDto.CreatedByUserName))]
    private static partial CalendarEventDto MapCalendarEventToDto(CalendarEvent source);

    // CalendarEvent -> CalendarEventSummaryDto (computed: IsRecurring, MemberCount)
    public static CalendarEventSummaryDto ToSummaryDto(CalendarEvent source)
    {
        var dto = MapCalendarEventToSummaryDto(source);
        dto.IsRecurring = !string.IsNullOrEmpty(source.RecurrenceRule);
        dto.MemberCount = source.Members.Count;
        return dto;
    }

    [MapperIgnoreTarget(nameof(CalendarEventSummaryDto.IsRecurring))]
    [MapperIgnoreTarget(nameof(CalendarEventSummaryDto.MemberCount))]
    private static partial CalendarEventSummaryDto MapCalendarEventToSummaryDto(CalendarEvent source);

    // CalendarEventMember -> CalendarEventMemberDto (computed: UserDisplayName)
    [UserMapping(Default = true)]
    public static CalendarEventMemberDto ToMemberDto(CalendarEventMember source)
    {
        var dto = MapCalendarEventMemberToDto(source);
        dto.UserDisplayName = source.User != null
            ? $"{source.User.FirstName} {source.User.LastName}".Trim()
            : string.Empty;
        return dto;
    }

    [MapperIgnoreTarget(nameof(CalendarEventMemberDto.UserDisplayName))]
    [MapperIgnoreTarget(nameof(CalendarEventMemberDto.ProfileImageUrl))]
    private static partial CalendarEventMemberDto MapCalendarEventMemberToDto(CalendarEventMember source);

    // CalendarEventException -> CalendarEventExceptionDto
    public static partial CalendarEventExceptionDto ToExceptionDto(CalendarEventException source);

    // CreateCalendarEventRequest -> CalendarEvent
    [MapperIgnoreTarget(nameof(CalendarEvent.Id))]
    [MapperIgnoreTarget(nameof(CalendarEvent.TenantId))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedByUserId))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEvent.UpdatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedByUser))]
    [MapperIgnoreTarget(nameof(CalendarEvent.Members))]
    [MapperIgnoreTarget(nameof(CalendarEvent.Exceptions))]
    public static partial CalendarEvent FromCreateRequest(CreateCalendarEventRequest source);

    // UpdateCalendarEventRequest -> CalendarEvent
    [MapperIgnoreTarget(nameof(CalendarEvent.Id))]
    [MapperIgnoreTarget(nameof(CalendarEvent.TenantId))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedByUserId))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEvent.UpdatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEvent.CreatedByUser))]
    [MapperIgnoreTarget(nameof(CalendarEvent.Members))]
    [MapperIgnoreTarget(nameof(CalendarEvent.Exceptions))]
    public static partial CalendarEvent FromUpdateRequest(UpdateCalendarEventRequest source);

    // CalendarEventMemberRequest -> CalendarEventMember
    [MapperIgnoreTarget(nameof(CalendarEventMember.Id))]
    [MapperIgnoreTarget(nameof(CalendarEventMember.CalendarEventId))]
    [MapperIgnoreTarget(nameof(CalendarEventMember.CreatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEventMember.UpdatedAt))]
    [MapperIgnoreTarget(nameof(CalendarEventMember.CalendarEvent))]
    [MapperIgnoreTarget(nameof(CalendarEventMember.User))]
    public static partial CalendarEventMember FromMemberRequest(CalendarEventMemberRequest source);

    // ExternalCalendarEvent -> CalendarOccurrenceDto (complex: subscription color, owner name, IsExternal = true)
    public static CalendarOccurrenceDto ToOccurrenceDto(ExternalCalendarEvent source)
    {
        var dto = MapExternalCalendarEventToOccurrenceDto(source);
        dto.EventId = source.Id;
        dto.IsExternal = true;
        dto.Color = source.Subscription != null ? source.Subscription.Color : null;
        dto.OwnerDisplayName = source.Subscription != null && source.Subscription.User != null
            ? $"{source.Subscription.User.FirstName} {source.Subscription.User.LastName}".Trim()
            : null;
        return dto;
    }

    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.EventId))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.Description))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.Location))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.Color))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.IsExternal))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.OriginalStartTimeUtc))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.Members))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.OwnerDisplayName))]
    [MapperIgnoreTarget(nameof(CalendarOccurrenceDto.OwnerProfileImageUrl))]
    private static partial CalendarOccurrenceDto MapExternalCalendarEventToOccurrenceDto(ExternalCalendarEvent source);
}
