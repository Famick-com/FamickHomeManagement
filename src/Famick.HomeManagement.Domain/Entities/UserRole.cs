using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Junction table for many-to-many relationship between users and roles.
/// A user can have multiple roles.
/// </summary>
public class UserRole : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID of the tenant this role assignment belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// ID of the user this role is assigned to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The role assigned to the user
    /// </summary>
    public Role Role { get; set; }

    // Navigation properties

    /// <summary>
    /// The user this role is assigned to
    /// </summary>
    public User User { get; set; } = null!;
}
