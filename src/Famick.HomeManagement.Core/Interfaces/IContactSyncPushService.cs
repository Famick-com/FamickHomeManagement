namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Sends silent push notifications to tenant devices when contacts are created, updated, or deleted.
/// Cloud implementation sends APNs/FCM silent pushes; self-hosted uses a no-op implementation.
/// </summary>
public interface IContactSyncPushService
{
    Task NotifyContactChangedAsync(Guid contactId, Guid tenantId, CancellationToken ct = default);
    Task NotifyContactDeletedAsync(Guid contactId, Guid tenantId, CancellationToken ct = default);
}
