using Android.Content;
using AndroidX.Work;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android WorkManager worker for periodic background contact sync.
/// </summary>
public class ContactSyncWorker : Worker
{
    private const string UniqueWorkName = "famick_contact_sync";

    public ContactSyncWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        if (!ContactSyncOrchestrator.ShouldSync(TimeSpan.FromHours(12)))
            return Result.InvokeSuccess();

        try
        {
            var orchestrator = App.Current?.Handler?.MauiContext?.Services.GetService<ContactSyncOrchestrator>();
            if (orchestrator == null)
            {
                // DI not available (process was killed) — retry later
                return Result.InvokeRetry();
            }

            var task = orchestrator.SyncAsync();
            task.GetAwaiter().GetResult();

            Console.WriteLine("[ContactSyncWorker] Background sync completed");
            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContactSyncWorker] Background sync failed: {ex.Message}");
            return Result.InvokeRetry();
        }
    }

    /// <summary>
    /// Schedules periodic contact sync (every 24 hours, requires network).
    /// </summary>
    public static void Schedule()
    {
        if (!ContactSyncOrchestrator.IsSyncEnabled)
            return;

        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected)
            .Build();

        var workRequest = new PeriodicWorkRequest.Builder(
                typeof(ContactSyncWorker),
                TimeSpan.FromHours(24))
            .SetConstraints(constraints)
            .Build();

        WorkManager.GetInstance(global::Android.App.Application.Context)
            .EnqueueUniquePeriodicWork(
                UniqueWorkName,
                ExistingPeriodicWorkPolicy.Keep!,
                workRequest);

        Console.WriteLine("[ContactSyncWorker] Scheduled periodic sync (24h interval)");
    }

    /// <summary>
    /// Cancels the scheduled periodic sync.
    /// </summary>
    public static void Cancel()
    {
        WorkManager.GetInstance(global::Android.App.Application.Context)
            .CancelUniqueWork(UniqueWorkName);

        Console.WriteLine("[ContactSyncWorker] Cancelled periodic sync");
    }
}
