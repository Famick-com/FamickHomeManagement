using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// No-op implementation for self-hosted deployments that have no push notification capability.
/// The cloud deployment overrides this with a real implementation.
/// </summary>
public class NullContactSyncPushService : IContactSyncPushService
{
    public Task NotifyContactChangedAsync(Guid contactId, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
    public Task NotifyContactDeletedAsync(Guid contactId, Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
}
