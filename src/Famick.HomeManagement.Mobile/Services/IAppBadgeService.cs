namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Sets the app-icon badge to an absolute value. On iOS this drives the springboard badge; on Android
/// it is a no-op (launcher-managed). Used to mirror the count of unread in-app notifications.
/// </summary>
public interface IAppBadgeService
{
    /// <summary>Sets the app-icon badge to <paramref name="count"/> (0 clears it).</summary>
    void SetBadge(int count);
}
