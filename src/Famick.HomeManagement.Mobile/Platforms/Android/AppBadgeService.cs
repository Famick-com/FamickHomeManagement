using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android app-icon badge setter — a no-op. Android launcher badges (dots/counts) are managed by the
/// launcher from active notifications and vary by device; there is no reliable cross-launcher API to
/// set an absolute count, so this intentionally does nothing.
/// </summary>
public class AppBadgeService : IAppBadgeService
{
    public void SetBadge(int count)
    {
        // Launcher-managed on Android; nothing to do.
    }
}
