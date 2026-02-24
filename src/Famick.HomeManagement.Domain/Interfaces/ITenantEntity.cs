namespace Famick.HomeManagement.Domain.Interfaces;

/// <summary>
/// Interface for entities that belong to a specific tenant (multi-tenancy support)
/// </summary>
public interface ITenantEntity : IEntity
{
    Guid TenantId { get; set; }
}
