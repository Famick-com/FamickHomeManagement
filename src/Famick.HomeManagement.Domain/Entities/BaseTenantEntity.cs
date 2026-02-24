using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Base class for all tenant-specific entities
/// </summary>
public abstract class BaseTenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
