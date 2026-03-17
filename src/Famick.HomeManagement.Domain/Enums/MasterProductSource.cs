namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Indicates how a master product was added to the global catalog.
/// </summary>
public enum MasterProductSource
{
    /// <summary>Product was seeded from embedded JSON data.</summary>
    Seeded = 0,

    /// <summary>Product was contributed by a tenant via ShareAsync.</summary>
    TenantContributed = 1,

    /// <summary>Product was created manually by a platform admin.</summary>
    AdminCreated = 2
}
