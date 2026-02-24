using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Provides access to the current tenant and user context using AsyncLocal for thread-safe storage
/// </summary>
public class TenantProvider : ITenantProvider
{
    private static readonly AsyncLocal<Guid?> _tenantId = new();
    private static readonly AsyncLocal<Guid?> _userId = new();

    public Guid? TenantId => _tenantId.Value;

    public Guid? UserId => _userId.Value;

    public void SetTenantId(Guid tenantId)
    {
        _tenantId.Value = tenantId;
    }

    public void SetUserId(Guid userId)
    {
        _userId.Value = userId;
    }

    public void ClearTenantId()
    {
        _tenantId.Value = null;
    }

    public void ClearUserId()
    {
        _userId.Value = null;
    }
}
