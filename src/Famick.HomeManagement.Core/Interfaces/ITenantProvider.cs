namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Provides access to the current tenant and user context
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the current tenant ID, or null if no tenant is set
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Gets the current user ID, or null if no user is authenticated
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Sets the current tenant ID
    /// </summary>
    void SetTenantId(Guid tenantId);

    /// <summary>
    /// Sets the current user ID
    /// </summary>
    void SetUserId(Guid userId);

    /// <summary>
    /// Clears the current tenant ID
    /// </summary>
    void ClearTenantId();

    /// <summary>
    /// Clears the current user ID
    /// </summary>
    void ClearUserId();
}
