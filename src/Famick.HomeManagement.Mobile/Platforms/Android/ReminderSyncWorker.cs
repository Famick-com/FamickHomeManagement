using Android.Content;
using AndroidX.Work;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android WorkManager worker for periodic offline-reminder prefetch. Re-fetches upcoming reminders
/// and reconciles the device's scheduled local notifications. Mirrors <see cref="CalendarSyncWorker"/>.
/// The orchestrator self-gates (enabled + self-hosted), so scheduling this unconditionally is safe.
/// </summary>
public class ReminderSyncWorker : Worker
{
    private const string UniqueWorkName = "famick_reminder_sync";

    public ReminderSyncWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        if (!NotificationSyncOrchestrator.ShouldSync(TimeSpan.FromMinutes(15)))
            return Result.InvokeSuccess();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<NotificationSyncOrchestrator>();
            if (orchestrator == null)
                return Result.InvokeRetry();

            orchestrator.SyncAsync().GetAwaiter().GetResult();

            Console.WriteLine("[ReminderSyncWorker] Background sync completed");
            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReminderSyncWorker] Background sync failed: {ex.Message}");
            return Result.InvokeRetry();
        }
    }

    /// <summary>Schedules periodic prefetch (every 6 hours, requires network).</summary>
    public static void Schedule()
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected)
            .Build();

        var workRequest = new PeriodicWorkRequest.Builder(
                typeof(ReminderSyncWorker),
                TimeSpan.FromHours(6))
            .SetConstraints(constraints)
            .Build();

        WorkManager.GetInstance(global::Android.App.Application.Context)
            .EnqueueUniquePeriodicWork(
                UniqueWorkName,
                ExistingPeriodicWorkPolicy.Keep!,
                workRequest);

        Console.WriteLine("[ReminderSyncWorker] Scheduled periodic sync (6h interval)");
    }

    /// <summary>Cancels the scheduled periodic prefetch.</summary>
    public static void Cancel()
    {
        WorkManager.GetInstance(global::Android.App.Application.Context)
            .CancelUniqueWork(UniqueWorkName);

        Console.WriteLine("[ReminderSyncWorker] Cancelled periodic sync");
    }
}
