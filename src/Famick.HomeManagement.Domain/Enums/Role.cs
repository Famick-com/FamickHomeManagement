namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// System-defined user roles for authorization
/// </summary>
public enum Role
{
    /// <summary>
    /// Read-only access to all data
    /// </summary>
    Viewer = 0,

    /// <summary>
    /// Can create, edit, and delete all data (products, inventory, shopping lists, etc.)
    /// </summary>
    Editor = 1,

    /// <summary>
    /// Full access including user management
    /// </summary>
    Admin = 2
}
