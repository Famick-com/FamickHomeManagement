using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAddressHasher _addressHasher;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IAddressHasher addressHasher,
        ILogger<TenantService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _addressHasher = addressHasher;
        _logger = logger;
    }

    public async Task<TenantDto?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.TenantId.HasValue)
        {
            _logger.LogWarning("No tenant context available");
            return null;
        }

        var tenant = await _context.Tenants
            .Include(t => t.Address)
            .FirstOrDefaultAsync(t => t.Id == _tenantProvider.TenantId.Value, cancellationToken);

        if (tenant == null)
        {
            _logger.LogWarning("Tenant not found for ID: {TenantId}", _tenantProvider.TenantId.Value);
            return null;
        }

        return TenantMapper.ToDto(tenant);
    }

    public async Task<TenantDto> UpdateCurrentTenantAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.TenantId.HasValue)
        {
            throw new InvalidOperationException("No tenant context available");
        }

        var tenant = await _context.Tenants
            .Include(t => t.Address)
            .FirstOrDefaultAsync(t => t.Id == _tenantProvider.TenantId.Value, cancellationToken);

        if (tenant == null)
        {
            throw new EntityNotFoundException("Tenant", _tenantProvider.TenantId.Value);
        }

        _logger.LogInformation("Updating tenant {TenantId} with name: {Name}", tenant.Id, request.Name);

        tenant.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            tenant.TimeZoneId = request.TimeZoneId;
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultEventColor))
        {
            tenant.DefaultEventColor = request.DefaultEventColor;
        }

        // Handle address update. The household's Address row is never edited in place: every
        // household that entered the address shares it (issue #119). The tenant points at the
        // row for the new address, and keeps its own unit lines. Only the components that
        // identify the address are taken from the request; a provider result the user picked
        // was recorded by the normalize call and is found by those components.
        if (request.Address != null)
        {
            var identity = new AddressIdentity(
                request.Address.AddressLine1, request.Address.City, request.Address.StateProvince,
                request.Address.PostalCode, request.Address.Country);
            var address = identity.IsEmpty
                ? null
                : await _context.FindOrCreateAsync(_addressHasher, identity, cancellationToken);

            tenant.Address = address;
            tenant.AddressId = address?.Id;
            tenant.AddressLine2 = NullIfBlank(request.Address.AddressLine2);
            tenant.AddressLine3 = NullIfBlank(request.Address.AddressLine3);
            tenant.AddressLine4 = NullIfBlank(request.Address.AddressLine4);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return TenantMapper.ToDto(tenant);
    }

    public async Task<Tenant> EnsureTenantExistsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            tenant = new Tenant
            {
                Id = tenantId,
                Name = "My Home"
            };
            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created tenant record with ID {TenantId}", tenantId);
        }

        return tenant;
    }

    public async Task<List<string>> GetDisabledPluginIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.TenantId.HasValue)
        {
            return new List<string>();
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantProvider.TenantId.Value, cancellationToken);

        if (tenant == null || string.IsNullOrWhiteSpace(tenant.DisabledPluginIds))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tenant.DisabledPluginIds) ?? new List<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse DisabledPluginIds for tenant {TenantId}", tenant.Id);
            return new List<string>();
        }
    }

    public async Task SetDisabledPluginIdsAsync(List<string> disabledIds, CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.TenantId.HasValue)
        {
            throw new InvalidOperationException("No tenant context available");
        }

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == _tenantProvider.TenantId.Value, cancellationToken);

        if (tenant == null)
        {
            throw new EntityNotFoundException("Tenant", _tenantProvider.TenantId.Value);
        }

        tenant.DisabledPluginIds = disabledIds.Count > 0
            ? JsonSerializer.Serialize(disabledIds)
            : null;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated disabled plugin IDs for tenant {TenantId}: {DisabledIds}",
            tenant.Id, tenant.DisabledPluginIds ?? "[]");
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
