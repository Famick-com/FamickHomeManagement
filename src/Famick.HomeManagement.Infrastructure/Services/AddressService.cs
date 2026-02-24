using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class AddressService : IAddressService
{
    private readonly HomeManagementDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<AddressService> _logger;

    public AddressService(
        HomeManagementDbContext db,
        ITenantProvider tenantProvider,
        IMapper mapper,
        ILogger<AddressService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<AddressDto>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var searchTerm = $"%{query.Trim()}%";
        limit = Math.Clamp(limit, 1, 25);

        // Get address IDs visible to the current tenant:
        // 1. Addresses linked via ContactAddress (tenant-filtered automatically)
        var contactAddressIds = _db.ContactAddresses
            .Select(ca => ca.AddressId);

        // 2. The tenant's own address
        var tenantAddressId = await _db.Set<Domain.Entities.Tenant>()
            .Where(t => t.Id == _tenantProvider.TenantId)
            .Select(t => t.AddressId)
            .FirstOrDefaultAsync(ct);

        // Combine into a single query
        var addressQuery = _db.Addresses
            .Where(a => contactAddressIds.Contains(a.Id)
                        || (tenantAddressId != null && a.Id == tenantAddressId));

        // Apply text search across key fields
        var results = await addressQuery
            .Where(a =>
                EF.Functions.ILike(a.AddressLine1 ?? "", searchTerm) ||
                EF.Functions.ILike(a.City ?? "", searchTerm) ||
                EF.Functions.ILike(a.StateProvince ?? "", searchTerm) ||
                EF.Functions.ILike(a.FormattedAddress ?? "", searchTerm))
            .OrderBy(a => a.AddressLine1)
            .Take(limit)
            .ToListAsync(ct);

        return _mapper.Map<List<AddressDto>>(results);
    }
}
