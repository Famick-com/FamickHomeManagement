using BackgroundTasks;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Registers and handles iOS background app refresh for the offline notification engine via
/// BGTaskScheduler. On each run it re-fetches upcoming reminders and reconciles the device's
/// scheduled local notifications. Mirrors <see cref="BackgroundCalendarSyncTask"/>.
///
/// Note: like all BGAppRefreshTasks, this does NOT run after the user force-quits the app — the
/// already-scheduled local notifications still fire, but the horizon is only extended when the app
/// is next foregrounded (which also triggers a sync).
/// </summary>
public static class BackgroundReminderSyncTask
{
    private const string TaskId = "com.famick.homemanagement.remindersync";

    /// <summary>Registers the handler with BGTaskScheduler. Must run before FinishedLaunching returns.</summary>
    public static void Register()
    {
        BGTaskScheduler.Shared.Register(TaskId, null, task =>
        {
            HandleBackgroundTask((BGAppRefreshTask)task);
        });
    }

    /// <summary>Schedules the next prefetch (~6 hours out) when offline reminders are enabled.</summary>
    public static void ScheduleNextSync()
    {
        if (!NotificationSyncOrchestrator.IsEnabled)
            return;

        var request = new BGAppRefreshTaskRequest(TaskId)
        {
            EarliestBeginDate = Foundation.NSDate.FromTimeIntervalSinceNow(6 * 60 * 60)
        };

        try
        {
            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error != null)
                Console.WriteLine($"[BackgroundReminderSync] Failed to schedule: {error}");
            else
                Console.WriteLine("[BackgroundReminderSync] Scheduled next sync in ~6 hours");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundReminderSync] Schedule error: {ex.Message}");
        }
    }

    /// <summary>Cancels any scheduled background prefetch.</summary>
    public static void CancelScheduledSync()
    {
        BGTaskScheduler.Shared.Cancel(TaskId);
        Console.WriteLine("[BackgroundReminderSync] Cancelled scheduled sync");
    }

    private static async void HandleBackgroundTask(BGAppRefreshTask task)
    {
        // Schedule the next run before doing work.
        ScheduleNextSync();

        if (!NotificationSyncOrchestrator.ShouldSync(TimeSpan.FromMinutes(15)))
        {
            task.SetTaskCompleted(true);
            return;
        }

        var cts = new CancellationTokenSource();
        task.ExpirationHandler = () => cts.Cancel();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<NotificationSyncOrchestrator>();
            if (orchestrator == null)
            {
                task.SetTaskCompleted(false);
                return;
            }

            await orchestrator.SyncAsync(cts.Token);
            task.SetTaskCompleted(true);
            Console.WriteLine("[BackgroundReminderSync] Background sync completed");
        }
        catch (OperationCanceledException)
        {
            task.SetTaskCompleted(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundReminderSync] Background sync failed: {ex.Message}");
            task.SetTaskCompleted(false);
        }
    }
}
