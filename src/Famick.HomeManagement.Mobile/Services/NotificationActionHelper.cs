namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Applies a user action taken on an OS notification back to the server. Currently: when the user
/// dismisses a notification (Android delete intent / iOS custom dismiss action), mark the
/// corresponding in-app notification read and refresh the app-icon badge.
/// </summary>
public static class NotificationActionHelper
{
    public static async Task MarkReadAsync(string? notificationId)
    {
        if (string.IsNullOrEmpty(notificationId) || !Guid.TryParse(notificationId, out var id))
            return;

        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            if (apiClient == null)
                return;

            await apiClient.MarkNotificationReadAsync(id);
            await AppBadgeHelper.RefreshAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationActionHelper] MarkRead failed: {ex.Message}");
        }
    }
}
