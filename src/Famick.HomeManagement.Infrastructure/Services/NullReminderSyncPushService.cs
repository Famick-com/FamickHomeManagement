using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// No-op implementation for self-hosted deployments that have no push notification capability.
/// The cloud deployment overrides this with a real silent-push implementation.
/// Self-hosted devices refresh their scheduled reminders via the periodic prefetch instead.
/// </summary>
public class NullReminderSyncPushService : IReminderSyncPushService
{
    public Task NotifyRemindersChangedAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
}
