using Android.Content;
using AndroidX.Work;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android WorkManager worker for periodic background calendar sync.
/// </summary>
public class CalendarSyncWorker : Worker
{
    private const string UniqueWorkName = "famick_calendar_sync";

    public CalendarSyncWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        if (!CalendarSyncOrchestrator.ShouldSync(TimeSpan.FromMinutes(15)))
            return Result.InvokeSuccess();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<CalendarSyncOrchestrator>();
            if (orchestrator == null)
                return Result.InvokeRetry();

            var task = orchestrator.SyncAsync();
            task.GetAwaiter().GetResult();

            Console.WriteLine("[CalendarSyncWorker] Background sync completed");
            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CalendarSyncWorker] Background sync failed: {ex.Message}");
            return Result.InvokeRetry();
        }
    }

    /// <summary>
    /// Schedules periodic calendar sync (every 12 hours, requires network).
    /// </summary>
    public static void Schedule()
    {
        if (!CalendarSyncOrchestrator.IsSyncEnabled)
            return;

        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected)
            .Build();

        var workRequest = new PeriodicWorkRequest.Builder(
                typeof(CalendarSyncWorker),
                TimeSpan.FromHours(12))
            .SetConstraints(constraints)
            .Build();

        WorkManager.GetInstance(global::Android.App.Application.Context)
            .EnqueueUniquePeriodicWork(
                UniqueWorkName,
                ExistingPeriodicWorkPolicy.Keep!,
                workRequest);

        Console.WriteLine("[CalendarSyncWorker] Scheduled periodic sync (12h interval)");
    }

    /// <summary>
    /// Cancels the scheduled periodic sync.
    /// </summary>
    public static void Cancel()
    {
        WorkManager.GetInstance(global::Android.App.Application.Context)
            .CancelUniqueWork(UniqueWorkName);

        Console.WriteLine("[CalendarSyncWorker] Cancelled periodic sync");
    }
}
