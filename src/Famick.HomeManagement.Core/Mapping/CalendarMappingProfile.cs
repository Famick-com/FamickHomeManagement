using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class CalendarMappingProfile : Profile
{
    public CalendarMappingProfile()
    {
        // CalendarEvent -> CalendarEventDto
        CreateMap<CalendarEvent, CalendarEventDto>()
            .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src =>
                src.CreatedByUser != null
                    ? $"{src.CreatedByUser.FirstName} {src.CreatedByUser.LastName}".Trim()
                    : null));

        // CalendarEvent -> CalendarEventSummaryDto
        CreateMap<CalendarEvent, CalendarEventSummaryDto>()
            .ForMember(dest => dest.IsRecurring, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.RecurrenceRule)))
            .ForMember(dest => dest.MemberCount, opt => opt.MapFrom(src => src.Members.Count));

        // CalendarEventMember -> CalendarEventMemberDto
        CreateMap<CalendarEventMember, CalendarEventMemberDto>()
            .ForMember(dest => dest.UserDisplayName, opt => opt.MapFrom(src =>
                src.User != null
                    ? $"{src.User.FirstName} {src.User.LastName}".Trim()
                    : string.Empty));

        // CalendarEventException -> CalendarEventExceptionDto
        CreateMap<CalendarEventException, CalendarEventExceptionDto>();

        // CreateCalendarEventRequest -> CalendarEvent
        CreateMap<CreateCalendarEventRequest, CalendarEvent>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByUser, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.Exceptions, opt => opt.Ignore());

        // UpdateCalendarEventRequest -> CalendarEvent
        CreateMap<UpdateCalendarEventRequest, CalendarEvent>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByUser, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.Exceptions, opt => opt.Ignore());

        // CalendarEventMemberRequest -> CalendarEventMember
        CreateMap<CalendarEventMemberRequest, CalendarEventMember>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CalendarEventId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CalendarEvent, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // ExternalCalendarEvent -> CalendarOccurrenceDto (for merged calendar view)
        CreateMap<ExternalCalendarEvent, CalendarOccurrenceDto>()
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Description, opt => opt.Ignore())
            .ForMember(dest => dest.Location, opt => opt.Ignore())
            .ForMember(dest => dest.Color, opt => opt.MapFrom(src =>
                src.Subscription != null ? src.Subscription.Color : null))
            .ForMember(dest => dest.IsExternal, opt => opt.MapFrom(_ => true))
            .ForMember(dest => dest.OriginalStartTimeUtc, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.OwnerDisplayName, opt => opt.MapFrom(src =>
                src.Subscription != null && src.Subscription.User != null
                    ? $"{src.Subscription.User.FirstName} {src.Subscription.User.LastName}".Trim()
                    : null))
            .ForMember(dest => dest.OwnerProfileImageUrl, opt => opt.Ignore());
    }
}
