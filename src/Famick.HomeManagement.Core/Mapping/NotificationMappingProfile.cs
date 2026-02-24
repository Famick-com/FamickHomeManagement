using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<Notification, NotificationDto>();

        CreateMap<UserDeviceToken, DeviceTokenDto>();
    }
}
