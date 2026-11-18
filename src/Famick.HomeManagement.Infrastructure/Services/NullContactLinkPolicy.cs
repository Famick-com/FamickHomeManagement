using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Nothing is linked. A single-household server has no other household to link with; the cloud
/// overrides this.
/// </summary>
public class NullContactLinkPolicy : IContactLinkPolicy
{
    private static readonly IReadOnlyDictionary<Guid, ContactLinkInfo> None = new Dictionary<Guid, ContactLinkInfo>();

    public Task<IReadOnlyDictionary<Guid, ContactLinkInfo>> GetLinkInfoAsync(
        IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default) => Task.FromResult(None);
}
