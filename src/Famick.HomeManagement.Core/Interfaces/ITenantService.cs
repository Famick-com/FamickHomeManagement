using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing tenant (household) information
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets the current tenant's information
    /// </summary>
    Task<TenantDto?> GetCurrentTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current tenant's name and address
    /// </summary>
    Task<TenantDto> UpdateCurrentTenantAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the tenant record exists for the given ID.
    /// Creates with default values if not found.
    /// Used during app startup for self-hosted mode.
    /// </summary>
    Task<Tenant> EnsureTenantExistsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of plugin IDs that an admin has disabled for the current tenant.
    /// Returns an empty list if none are disabled.
    /// </summary>
    Task<List<string>> GetDisabledPluginIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the list of disabled plugin IDs for the current tenant.
    /// Pass an empty list to re-enable all plugins.
    /// </summary>
    Task SetDisabledPluginIdsAsync(List<string> disabledIds, CancellationToken cancellationToken = default);
}
