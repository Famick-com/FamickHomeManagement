#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Widget provider for the Quick Consume home screen widget.
/// Displays a barcode scanner icon with expired/expiring item counts.
/// Tapping the widget opens the app to the Quick Consume page via deep link.
/// </summary>
[BroadcastReceiver(Label = "Quick Consume", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_quick_consume_info")]
public class QuickConsumeWidgetProvider : AppWidgetProvider
{
    private const string PrefsName = "com.famick.homemanagement.widgets";
    private const string DeepLinkUrl = "famick://quick-consume";

    /// <summary>
    /// Called when the widget needs to be updated.
    /// </summary>
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null)
        {
            return;
        }

        foreach (var widgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, widgetId);
        }
    }

    /// <summary>
    /// Called when the first widget is placed on the home screen.
    /// </summary>
    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        Console.WriteLine("[QuickConsumeWidget] Widget enabled");
    }

    /// <summary>
    /// Called when the last widget is removed from the home screen.
    /// </summary>
    public override void OnDisabled(Context? context)
    {
        base.OnDisabled(context);
        Console.WriteLine("[QuickConsumeWidget] Widget disabled");
    }

    /// <summary>
    /// Updates a single widget instance with current data.
    /// </summary>
    private void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
    {
        try
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_quick_consume);

            // Load stats from SharedPreferences
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var expiringCount = prefs?.GetInt("expiring_count", 0) ?? 0;
            var dueSoonCount = prefs?.GetInt("due_soon_count", 0) ?? 0;

            // Update the stats display
            if (expiringCount > 0 || dueSoonCount > 0)
            {
                views.SetViewVisibility(Resource.Id.widget_stats, global::Android.Views.ViewStates.Visible);

                if (expiringCount > 0)
                {
                    views.SetViewVisibility(Resource.Id.expired_badge, global::Android.Views.ViewStates.Visible);
                    views.SetTextViewText(Resource.Id.expired_count, expiringCount.ToString());
                }
                else
                {
                    views.SetViewVisibility(Resource.Id.expired_badge, global::Android.Views.ViewStates.Gone);
                }

                if (dueSoonCount > 0)
                {
                    views.SetViewVisibility(Resource.Id.due_soon_badge, global::Android.Views.ViewStates.Visible);
                    views.SetTextViewText(Resource.Id.due_soon_count, dueSoonCount.ToString());
                }
                else
                {
                    views.SetViewVisibility(Resource.Id.due_soon_badge, global::Android.Views.ViewStates.Gone);
                }
            }
            else
            {
                views.SetViewVisibility(Resource.Id.widget_stats, global::Android.Views.ViewStates.Gone);
            }

            // Create pending intent for deep link to Quick Consume page
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(DeepLinkUrl));
            intent.SetPackage(context.PackageName);
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

            // Use Immutable flag on Android M (API 23+) for security, UpdateCurrent on older versions
            var flags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
#pragma warning disable CA1416 // Validate platform compatibility - SDK version check above ensures this is safe
                flags |= PendingIntentFlags.Immutable;
#pragma warning restore CA1416
            }

            var pendingIntent = PendingIntent.GetActivity(
                context,
                widgetId,
                intent,
                flags);

            views.SetOnClickPendingIntent(Resource.Id.widget_layout, pendingIntent);

            // Update the widget
            appWidgetManager.UpdateAppWidget(widgetId, views);

            Console.WriteLine($"[QuickConsumeWidget] Updated widget {widgetId}: Expiring={expiringCount}, DueSoon={dueSoonCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuickConsumeWidget] Error updating widget {widgetId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Static helper to trigger widget updates from anywhere in the app.
    /// Call this after updating widget data.
    /// </summary>
    public static void UpdateAllWidgets(Context context)
    {
        try
        {
            var appWidgetManager = AppWidgetManager.GetInstance(context);
            var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(QuickConsumeWidgetProvider)));
            var widgetIds = appWidgetManager?.GetAppWidgetIds(componentName);

            if (widgetIds != null && widgetIds.Length > 0)
            {
                var intent = new Intent(context, typeof(QuickConsumeWidgetProvider));
                intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
                intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, widgetIds);
                context.SendBroadcast(intent);

                Console.WriteLine($"[QuickConsumeWidget] Triggered update for {widgetIds.Length} widgets");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuickConsumeWidget] Error triggering widget updates: {ex.Message}");
        }
    }
}
#endif
