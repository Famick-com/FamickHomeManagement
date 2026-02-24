namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Push notification device platform.
/// </summary>
public enum DevicePlatform
{
    /// <summary>
    /// Apple Push Notification service (APNs)
    /// </summary>
    iOS = 1,

    /// <summary>
    /// Firebase Cloud Messaging (FCM)
    /// </summary>
    Android = 2
}
