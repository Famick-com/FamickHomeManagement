namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a permission/capability in the system
/// Permissions are global (not tenant-specific)
/// </summary>
public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
