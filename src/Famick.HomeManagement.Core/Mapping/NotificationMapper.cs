#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class NotificationMapper
{
    public static partial NotificationDto ToDto(Notification source);

    public static partial DeviceTokenDto ToDeviceTokenDto(UserDeviceToken source);
}
