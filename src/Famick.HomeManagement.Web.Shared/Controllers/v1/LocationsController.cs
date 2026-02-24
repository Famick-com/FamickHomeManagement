using Famick.HomeManagement.Core.DTOs.Locations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing storage locations
/// </summary>
[ApiController]
[Route("api/v1/locations")]
[Authorize]
public class LocationsController : ApiControllerBase
{
    private readonly HomeManagementDbContext _dbContext;

    public LocationsController(
        HomeManagementDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<LocationsController> logger)
        : base(tenantProvider, logger)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists all locations for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<LocationDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing locations for tenant {TenantId}", TenantId);

        var locations = await _dbContext.Locations
            .Where(l => l.TenantId == TenantId)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .Select(l => new LocationDto
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                IsActive = l.IsActive,
                SortOrder = l.SortOrder
            })
            .ToListAsync(cancellationToken);

        return ApiResponse(locations);
    }

    /// <summary>
    /// Gets a specific location by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LocationDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting location {LocationId} for tenant {TenantId}", id, TenantId);

        var location = await _dbContext.Locations
            .Where(l => l.Id == id && l.TenantId == TenantId)
            .Select(l => new LocationDto
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                IsActive = l.IsActive,
                SortOrder = l.SortOrder
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location == null)
        {
            return NotFoundResponse($"Location with ID {id} not found");
        }

        return ApiResponse(location);
    }

    /// <summary>
    /// Creates a new location
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(LocationDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error_message = "Name is required" });
        }

        _logger.LogInformation("Creating location '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var location = new Location
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            Description = location.Description,
            IsActive = location.IsActive,
            SortOrder = location.SortOrder
        };

        return CreatedAtAction(nameof(GetById), new { id = location.Id }, dto);
    }

    /// <summary>
    /// Updates an existing location
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(LocationDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error_message = "Name is required" });
        }

        _logger.LogInformation("Updating location {LocationId} for tenant {TenantId}", id, TenantId);

        var location = await _dbContext.Locations
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, cancellationToken);

        if (location == null)
        {
            return NotFoundResponse($"Location with ID {id} not found");
        }

        location.Name = request.Name.Trim();
        location.Description = request.Description?.Trim();
        location.SortOrder = request.SortOrder;
        location.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            Description = location.Description,
            IsActive = location.IsActive,
            SortOrder = location.SortOrder
        };

        return ApiResponse(dto);
    }

    /// <summary>
    /// Deletes a location
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting location {LocationId} for tenant {TenantId}", id, TenantId);

        var location = await _dbContext.Locations
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, cancellationToken);

        if (location == null)
        {
            return NotFoundResponse($"Location with ID {id} not found");
        }

        // Check if location is in use by any products
        var inUse = await _dbContext.Products
            .AnyAsync(p => p.LocationId == id && p.TenantId == TenantId, cancellationToken);

        if (inUse)
        {
            return Conflict(new { error_message = "Cannot delete location that is in use by products" });
        }

        _dbContext.Locations.Remove(location);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
