namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Visibility levels for contact records
/// </summary>
public enum ContactVisibilityLevel
{
    /// <summary>
    /// Contact is visible to all users in the tenant
    /// </summary>
    TenantShared = 0,

    /// <summary>
    /// Contact is only visible to the user who created it
    /// </summary>
    UserPrivate = 1,

    /// <summary>
    /// Contact is visible to the creator and specifically shared users
    /// </summary>
    SharedWithUsers = 2
}
