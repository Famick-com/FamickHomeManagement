namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Refreshes the app-icon badge from the server's unread-notification count so the badge mirrors the
/// in-app notifications list. Called on app resume and after the user reads/dismisses notifications;
/// the server also stamps the badge onto outgoing pushes so it updates while the app is closed.
/// </summary>
public static class AppBadgeHelper
{
    public static async Task RefreshAsync()
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            var badge = services?.GetService<IAppBadgeService>();
            if (apiClient == null || badge == null)
                return;

            var result = await apiClient.GetUnreadNotificationCountAsync().ConfigureAwait(false);
            if (result.Success && result.Data != null)
                badge.SetBadge(result.Data.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppBadgeHelper] Refresh failed: {ex.Message}");
        }
    }
}
