using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// No-op implementation for deployments that keep no copies of contacts elsewhere. The cloud
/// deployment overrides this.
/// </summary>
public class NullContactChangeSink : IContactChangeSink
{
    public Task ContactsChangedAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default) =>
        Task.CompletedTask;
}
