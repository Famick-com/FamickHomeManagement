namespace Famick.HomeManagement.Infrastructure.Configuration;

/// <summary>
/// Configuration options for multi-tenancy behavior.
/// Allows the system to operate in either single-tenant (self-hosted) or multi-tenant (cloud) mode.
/// </summary>
public interface IMultiTenancyOptions
{
    /// <summary>
    /// Gets a value indicating whether multi-tenancy is enabled.
    /// When false, the system operates in single-tenant mode with a fixed tenant ID.
    /// When true, the system operates in multi-tenant mode with dynamic tenant resolution.
    /// </summary>
    bool IsMultiTenantEnabled { get; }

    /// <summary>
    /// Gets the fixed tenant ID to use when multi-tenancy is disabled (self-hosted mode).
    /// This value is ignored when IsMultiTenantEnabled is true.
    /// </summary>
    Guid? FixedTenantId { get; }
}

/// <summary>
/// Default implementation of multi-tenancy configuration options.
/// </summary>
public class MultiTenancyOptions : IMultiTenancyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether multi-tenancy is enabled.
    /// Default is false (single-tenant mode).
    /// </summary>
    public bool IsMultiTenantEnabled { get; set; }

    /// <summary>
    /// Gets or sets the fixed tenant ID for single-tenant mode.
    /// Default is null (no fixed tenant).
    /// </summary>
    public Guid? FixedTenantId { get; set; }
}
