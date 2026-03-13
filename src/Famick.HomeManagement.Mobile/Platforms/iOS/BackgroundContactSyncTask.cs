using BackgroundTasks;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Registers and handles iOS background app refresh for contact sync via BGTaskScheduler.
/// </summary>
public static class BackgroundContactSyncTask
{
    private const string TaskId = "com.famick.homemanagement.contactsync";

    /// <summary>
    /// Registers the background task handler with BGTaskScheduler.
    /// Must be called before FinishedLaunching returns.
    /// </summary>
    public static void Register()
    {
        BGTaskScheduler.Shared.Register(TaskId, null, task =>
        {
            HandleBackgroundTask((BGAppRefreshTask)task);
        });
    }

    /// <summary>
    /// Schedules the next background sync with a 12-hour earliest begin date.
    /// Only schedules if contact sync is enabled.
    /// </summary>
    public static void ScheduleNextSync()
    {
        if (!ContactSyncOrchestrator.IsSyncEnabled)
            return;

        var request = new BGAppRefreshTaskRequest(TaskId)
        {
            EarliestBeginDate = Foundation.NSDate.FromTimeIntervalSinceNow(12 * 60 * 60) // 12 hours
        };

        try
        {
            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error != null)
                Console.WriteLine($"[BackgroundContactSync] Failed to schedule: {error}");
            else
                Console.WriteLine("[BackgroundContactSync] Scheduled next sync in ~12 hours");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundContactSync] Schedule error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancels any scheduled background sync task.
    /// </summary>
    public static void CancelScheduledSync()
    {
        BGTaskScheduler.Shared.Cancel(TaskId);
        Console.WriteLine("[BackgroundContactSync] Cancelled scheduled sync");
    }

    private static async void HandleBackgroundTask(BGAppRefreshTask task)
    {
        // Schedule the next sync before starting work
        ScheduleNextSync();

        if (!ContactSyncOrchestrator.ShouldSync(TimeSpan.FromHours(12)))
        {
            task.SetTaskCompleted(true);
            return;
        }

        var cts = new CancellationTokenSource();
        task.ExpirationHandler = () => cts.Cancel();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<ContactSyncOrchestrator>();
            if (orchestrator == null)
            {
                task.SetTaskCompleted(false);
                return;
            }

            await orchestrator.SyncAsync(cts.Token);
            task.SetTaskCompleted(true);
            Console.WriteLine("[BackgroundContactSync] Background sync completed");
        }
        catch (OperationCanceledException)
        {
            task.SetTaskCompleted(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundContactSync] Background sync failed: {ex.Message}");
            task.SetTaskCompleted(false);
        }
    }
}
