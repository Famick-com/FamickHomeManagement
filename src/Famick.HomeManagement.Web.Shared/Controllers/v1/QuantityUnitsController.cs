using Famick.HomeManagement.Core.DTOs.QuantityUnits;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing quantity units
/// </summary>
[ApiController]
[Route("api/v1/quantity-units")]
[Authorize]
public class QuantityUnitsController : ApiControllerBase
{
    private readonly HomeManagementDbContext _dbContext;

    public QuantityUnitsController(
        HomeManagementDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<QuantityUnitsController> logger)
        : base(tenantProvider, logger)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists all quantity units for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<QuantityUnitDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing quantity units for tenant {TenantId}", TenantId);

        var units = await _dbContext.QuantityUnits
            .Where(u => u.TenantId == TenantId)
            .OrderBy(u => u.Name)
            .Select(u => new QuantityUnitDto
            {
                Id = u.Id,
                Name = u.Name,
                NamePlural = u.NamePlural,
                Description = u.Description,
                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken);

        return ApiResponse(units);
    }

    /// <summary>
    /// Gets a specific quantity unit by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(QuantityUnitDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting quantity unit {UnitId} for tenant {TenantId}", id, TenantId);

        var unit = await _dbContext.QuantityUnits
            .Where(u => u.Id == id && u.TenantId == TenantId)
            .Select(u => new QuantityUnitDto
            {
                Id = u.Id,
                Name = u.Name,
                NamePlural = u.NamePlural,
                Description = u.Description,
                IsActive = u.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (unit == null)
        {
            return NotFoundResponse($"Quantity unit with ID {id} not found");
        }

        return ApiResponse(unit);
    }

    /// <summary>
    /// Creates a new quantity unit
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(QuantityUnitDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateQuantityUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error_message = "Name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.NamePlural))
        {
            return BadRequest(new { error_message = "Plural name is required" });
        }

        _logger.LogInformation("Creating quantity unit '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var unit = new QuantityUnit
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = request.Name.Trim(),
            NamePlural = request.NamePlural.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };

        _dbContext.QuantityUnits.Add(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new QuantityUnitDto
        {
            Id = unit.Id,
            Name = unit.Name,
            NamePlural = unit.NamePlural,
            Description = unit.Description,
            IsActive = unit.IsActive
        };

        return CreatedAtAction(nameof(GetById), new { id = unit.Id }, dto);
    }

    /// <summary>
    /// Updates an existing quantity unit
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(QuantityUnitDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateQuantityUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error_message = "Name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.NamePlural))
        {
            return BadRequest(new { error_message = "Plural name is required" });
        }

        _logger.LogInformation("Updating quantity unit {UnitId} for tenant {TenantId}", id, TenantId);

        var unit = await _dbContext.QuantityUnits
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, cancellationToken);

        if (unit == null)
        {
            return NotFoundResponse($"Quantity unit with ID {id} not found");
        }

        unit.Name = request.Name.Trim();
        unit.NamePlural = request.NamePlural.Trim();
        unit.Description = request.Description?.Trim();
        unit.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new QuantityUnitDto
        {
            Id = unit.Id,
            Name = unit.Name,
            NamePlural = unit.NamePlural,
            Description = unit.Description,
            IsActive = unit.IsActive
        };

        return ApiResponse(dto);
    }

    /// <summary>
    /// Deletes a quantity unit
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting quantity unit {UnitId} for tenant {TenantId}", id, TenantId);

        var unit = await _dbContext.QuantityUnits
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, cancellationToken);

        if (unit == null)
        {
            return NotFoundResponse($"Quantity unit with ID {id} not found");
        }

        // Check if unit is in use by any products
        var inUseAsPurchase = await _dbContext.Products
            .AnyAsync(p => p.QuantityUnitIdPurchase == id && p.TenantId == TenantId, cancellationToken);

        var inUseAsStock = await _dbContext.Products
            .AnyAsync(p => p.QuantityUnitIdStock == id && p.TenantId == TenantId, cancellationToken);

        if (inUseAsPurchase || inUseAsStock)
        {
            return Conflict(new { error_message = "Cannot delete quantity unit that is in use by products" });
        }

        _dbContext.QuantityUnits.Remove(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
