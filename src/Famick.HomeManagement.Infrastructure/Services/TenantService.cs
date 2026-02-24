using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Tenant;
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
    private readonly IMapper _mapper;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IMapper mapper,
        ILogger<TenantService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
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

        return _mapper.Map<TenantDto>(tenant);
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

        // Handle address update
        if (request.Address != null)
        {
            if (tenant.Address == null)
            {
                // Create new address
                var address = _mapper.Map<Address>(request.Address);
                address.Id = Guid.NewGuid();
                address.NormalizedHash = ComputeAddressHash(request.Address);
                _context.Addresses.Add(address);
                tenant.AddressId = address.Id;
                tenant.Address = address;
                _logger.LogInformation("Created new address for tenant {TenantId}", tenant.Id);
            }
            else
            {
                // Update existing address
                _mapper.Map(request.Address, tenant.Address);
                tenant.Address.NormalizedHash = ComputeAddressHash(request.Address);
                _logger.LogInformation("Updated address for tenant {TenantId}", tenant.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TenantDto>(tenant);
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

    private static string? ComputeAddressHash(UpdateAddressRequest address)
    {
        var parts = new[]
        {
            address.AddressLine1?.Trim().ToLowerInvariant(),
            address.City?.Trim().ToLowerInvariant(),
            address.StateProvince?.Trim().ToLowerInvariant(),
            address.PostalCode?.Trim().ToLowerInvariant(),
            address.Country?.Trim().ToLowerInvariant()
        };

        var combined = string.Join("|", parts.Where(p => !string.IsNullOrEmpty(p)));
        if (string.IsNullOrEmpty(combined)) return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
