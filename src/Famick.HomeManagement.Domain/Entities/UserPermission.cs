using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Junction table for User-Permission many-to-many relationship
/// </summary>
public class UserPermission : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
