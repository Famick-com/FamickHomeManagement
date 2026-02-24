using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class HomeService : IHomeService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<HomeService> _logger;

    public HomeService(
        HomeManagementDbContext context,
        IMapper mapper,
        ILogger<HomeService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<HomeDto?> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting home for current tenant");

        var home = await _context.Homes
            .Include(h => h.Utilities)
            .FirstOrDefaultAsync(cancellationToken);

        if (home == null)
        {
            return null;
        }

        return _mapper.Map<HomeDto>(home);
    }

    public async Task<bool> IsHomeSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);
        return home?.IsSetupComplete ?? false;
    }

    public async Task<HomeDto> SetupHomeAsync(
        HomeSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting up home for tenant");

        // Check if home already exists
        var existingHome = await _context.Homes.FirstOrDefaultAsync(cancellationToken);
        if (existingHome != null)
        {
            throw new DuplicateEntityException("Home", "TenantId", "current tenant");
        }

        var home = _mapper.Map<Home>(request);
        home.Id = Guid.NewGuid();
        home.IsSetupComplete = true;

        _context.Homes.Add(home);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Home setup complete: {Id}", home.Id);

        return _mapper.Map<HomeDto>(home);
    }

    public async Task<HomeDto> UpdateHomeAsync(
        UpdateHomeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating home");

        var home = await _context.Homes
            .Include(h => h.Utilities)
            .FirstOrDefaultAsync(cancellationToken);

        if (home == null)
        {
            throw new EntityNotFoundException("Home", Guid.Empty);
        }

        _mapper.Map(request, home);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Home updated: {Id}", home.Id);

        return _mapper.Map<HomeDto>(home);
    }

    public async Task<HomeUtilityDto> AddUtilityAsync(
        CreateHomeUtilityRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding utility: {UtilityType}", request.UtilityType);

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);
        if (home == null)
        {
            throw new EntityNotFoundException("Home", Guid.Empty);
        }

        // Check if utility of this type already exists
        var existingUtility = await _context.HomeUtilities
            .FirstOrDefaultAsync(u => u.HomeId == home.Id && u.UtilityType == request.UtilityType, cancellationToken);

        if (existingUtility != null)
        {
            throw new DuplicateEntityException("HomeUtility", "UtilityType", request.UtilityType.ToString());
        }

        var utility = _mapper.Map<HomeUtility>(request);
        utility.Id = Guid.NewGuid();
        utility.HomeId = home.Id;

        _context.HomeUtilities.Add(utility);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Utility added: {Id} ({UtilityType})", utility.Id, utility.UtilityType);

        return _mapper.Map<HomeUtilityDto>(utility);
    }

    public async Task<HomeUtilityDto> UpdateUtilityAsync(
        Guid utilityId,
        UpdateHomeUtilityRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating utility: {UtilityId}", utilityId);

        var utility = await _context.HomeUtilities
            .FirstOrDefaultAsync(u => u.Id == utilityId, cancellationToken);

        if (utility == null)
        {
            throw new EntityNotFoundException(nameof(HomeUtility), utilityId);
        }

        _mapper.Map(request, utility);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Utility updated: {Id}", utility.Id);

        return _mapper.Map<HomeUtilityDto>(utility);
    }

    public async Task DeleteUtilityAsync(
        Guid utilityId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting utility: {UtilityId}", utilityId);

        var utility = await _context.HomeUtilities
            .FirstOrDefaultAsync(u => u.Id == utilityId, cancellationToken);

        if (utility == null)
        {
            throw new EntityNotFoundException(nameof(HomeUtility), utilityId);
        }

        _context.HomeUtilities.Remove(utility);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Utility deleted: {Id}", utilityId);
    }

    #region Property Links

    public async Task<List<PropertyLinkDto>> GetPropertyLinksAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting property links");

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);
        if (home == null)
        {
            return new List<PropertyLinkDto>();
        }

        var links = await _context.PropertyLinks
            .Where(p => p.HomeId == home.Id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<PropertyLinkDto>>(links);
    }

    public async Task<PropertyLinkDto> AddPropertyLinkAsync(
        CreatePropertyLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding property link: {Label}", request.Label);

        var home = await _context.Homes.FirstOrDefaultAsync(cancellationToken);
        if (home == null)
        {
            throw new EntityNotFoundException("Home", Guid.Empty);
        }

        // Get next sort order if not provided
        var sortOrder = request.SortOrder;
        if (!sortOrder.HasValue)
        {
            var maxOrder = await _context.PropertyLinks
                .Where(p => p.HomeId == home.Id)
                .MaxAsync(p => (int?)p.SortOrder, cancellationToken) ?? 0;
            sortOrder = maxOrder + 1;
        }

        var link = new PropertyLink
        {
            Id = Guid.NewGuid(),
            HomeId = home.Id,
            Url = request.Url,
            Label = request.Label,
            SortOrder = sortOrder.Value
        };

        _context.PropertyLinks.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Property link added: {Id}", link.Id);

        return _mapper.Map<PropertyLinkDto>(link);
    }

    public async Task<PropertyLinkDto> UpdatePropertyLinkAsync(
        Guid linkId,
        UpdatePropertyLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating property link: {LinkId}", linkId);

        var link = await _context.PropertyLinks
            .FirstOrDefaultAsync(p => p.Id == linkId, cancellationToken);

        if (link == null)
        {
            throw new EntityNotFoundException(nameof(PropertyLink), linkId);
        }

        link.Url = request.Url;
        link.Label = request.Label;
        link.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Property link updated: {Id}", link.Id);

        return _mapper.Map<PropertyLinkDto>(link);
    }

    public async Task DeletePropertyLinkAsync(
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting property link: {LinkId}", linkId);

        var link = await _context.PropertyLinks
            .FirstOrDefaultAsync(p => p.Id == linkId, cancellationToken);

        if (link == null)
        {
            throw new EntityNotFoundException(nameof(PropertyLink), linkId);
        }

        _context.PropertyLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Property link deleted: {Id}", linkId);
    }

    #endregion
}
