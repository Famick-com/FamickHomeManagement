using Android.App;
using Android.Content;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Re-arms scheduled reminder alarms after a device reboot. AlarmManager alarms do not survive a
/// reboot, so on BOOT_COMPLETED we replay everything still pending from the local store (no network).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Intent.ActionBootCompleted) return;

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var orchestrator = IPlatformApplication.Current?.Services
                    .GetService<NotificationSyncOrchestrator>();
                if (orchestrator != null)
                    await orchestrator.RearmFromStoreAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BootReceiver] Re-arm failed: {ex.Message}");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
