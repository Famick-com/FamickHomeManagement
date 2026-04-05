using BackgroundTasks;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Registers and handles iOS background app refresh for calendar sync via BGTaskScheduler.
/// </summary>
public static class BackgroundCalendarSyncTask
{
    private const string TaskId = "com.famick.homemanagement.calendarsync";

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
    /// Schedules the next background sync with a 6-hour earliest begin date.
    /// Only schedules if calendar sync is enabled.
    /// </summary>
    public static void ScheduleNextSync()
    {
        if (!CalendarSyncOrchestrator.IsSyncEnabled)
            return;

        var request = new BGAppRefreshTaskRequest(TaskId)
        {
            EarliestBeginDate = Foundation.NSDate.FromTimeIntervalSinceNow(6 * 60 * 60) // 6 hours
        };

        try
        {
            BGTaskScheduler.Shared.Submit(request, out var error);
            if (error != null)
                Console.WriteLine($"[BackgroundCalendarSync] Failed to schedule: {error}");
            else
                Console.WriteLine("[BackgroundCalendarSync] Scheduled next sync in ~6 hours");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundCalendarSync] Schedule error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancels any scheduled background sync task.
    /// </summary>
    public static void CancelScheduledSync()
    {
        BGTaskScheduler.Shared.Cancel(TaskId);
        Console.WriteLine("[BackgroundCalendarSync] Cancelled scheduled sync");
    }

    private static async void HandleBackgroundTask(BGAppRefreshTask task)
    {
        // Schedule the next sync before starting work
        ScheduleNextSync();

        if (!CalendarSyncOrchestrator.ShouldSync(TimeSpan.FromMinutes(15)))
        {
            task.SetTaskCompleted(true);
            return;
        }

        var cts = new CancellationTokenSource();
        task.ExpirationHandler = () => cts.Cancel();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<CalendarSyncOrchestrator>();
            if (orchestrator == null)
            {
                task.SetTaskCompleted(false);
                return;
            }

            await orchestrator.SyncAsync(cts.Token);
            task.SetTaskCompleted(true);
            Console.WriteLine("[BackgroundCalendarSync] Background sync completed");
        }
        catch (OperationCanceledException)
        {
            task.SetTaskCompleted(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundCalendarSync] Background sync failed: {ex.Message}");
            task.SetTaskCompleted(false);
        }
    }
}
