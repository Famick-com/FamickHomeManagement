#if ANDROID
using Android.Content;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Service for sharing data with Android widgets via SharedPreferences.
/// </summary>
public static class WidgetDataService
{
    private const string PrefsName = "com.famick.homemanagement.widgets";

    /// <summary>
    /// Updates widget data in SharedPreferences for Android widgets to read.
    /// </summary>
    /// <param name="context">Android context</param>
    /// <param name="expiringCount">Number of expired items</param>
    /// <param name="dueSoonCount">Number of items expiring soon</param>
    public static void UpdateWidgetData(Context? context, int expiringCount, int dueSoonCount)
    {
        try
        {
            if (context == null)
            {
                Console.WriteLine("[WidgetDataService] Context is null, cannot update widget data");
                return;
            }

            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            if (prefs == null)
            {
                Console.WriteLine("[WidgetDataService] Failed to access SharedPreferences");
                return;
            }

            var editor = prefs.Edit();
            if (editor == null)
            {
                Console.WriteLine("[WidgetDataService] Failed to get SharedPreferences editor");
                return;
            }

            editor.PutInt("expiring_count", expiringCount);
            editor.PutInt("due_soon_count", dueSoonCount);
            editor.PutLong("last_updated", Java.Lang.JavaSystem.CurrentTimeMillis());
            editor.Apply();

            Console.WriteLine($"[WidgetDataService] Updated widget data: Expiring={expiringCount}, DueSoon={dueSoonCount}");

            // Trigger widget refresh
            QuickConsumeWidgetProvider.UpdateAllWidgets(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WidgetDataService] Error updating widget data: {ex.Message}");
        }
    }
}
#endif
