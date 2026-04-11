using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductGroupService : IProductGroupService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<ProductGroupService> _logger;

    public ProductGroupService(
        HomeManagementDbContext context,
        ILogger<ProductGroupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductGroupDto> CreateAsync(
        CreateProductGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating product group: {Name}", request.Name);

        // Check for duplicate name
        var exists = await _context.ProductGroups
            .AnyAsync(pg => pg.Name == request.Name, cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(ProductGroup), "Name", request.Name);
        }

        var productGroup = ProductGroupMapper.FromCreateRequest(request);
        productGroup.Id = Guid.NewGuid();

        _context.ProductGroups.Add(productGroup);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created product group: {Id} - {Name}", productGroup.Id, productGroup.Name);

        return ProductGroupMapper.ToDto(productGroup);
    }

    public async Task<ProductGroupDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var productGroup = await _context.ProductGroups
            .Include(pg => pg.Products)
            .FirstOrDefaultAsync(pg => pg.Id == id, cancellationToken);

        return productGroup is not null ? ProductGroupMapper.ToDto(productGroup) : null;
    }

    public async Task<List<ProductGroupDto>> ListAsync(
        ProductGroupFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProductGroups
            .Include(pg => pg.Products)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(pg =>
                pg.Name.ToLower().Contains(searchTerm) ||
                (pg.Description != null && pg.Description.ToLower().Contains(searchTerm)));
        }

        // Apply sorting
        query = (filter?.SortBy?.ToLower()) switch
        {
            "name" => filter.Descending
                ? query.OrderByDescending(pg => pg.Name)
                : query.OrderBy(pg => pg.Name),
            "createdat" => filter.Descending
                ? query.OrderByDescending(pg => pg.CreatedAt)
                : query.OrderBy(pg => pg.CreatedAt),
            "productcount" => filter.Descending
                ? query.OrderByDescending(pg => pg.Products!.Count)
                : query.OrderBy(pg => pg.Products!.Count),
            _ => query.OrderBy(pg => pg.Name) // Default sort by name
        };

        var productGroups = await query.ToListAsync(cancellationToken);

        return productGroups.Select(ProductGroupMapper.ToDto).ToList();
    }

    public async Task<ProductGroupDto> UpdateAsync(
        Guid id,
        UpdateProductGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating product group: {Id}", id);

        var productGroup = await _context.ProductGroups.FindAsync(new object[] { id }, cancellationToken);
        if (productGroup == null)
        {
            throw new EntityNotFoundException(nameof(ProductGroup), id);
        }

        // Check for duplicate name (excluding current entity)
        var exists = await _context.ProductGroups
            .AnyAsync(pg => pg.Name == request.Name && pg.Id != id, cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(ProductGroup), "Name", request.Name);
        }

        ProductGroupMapper.Update(request, productGroup);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated product group: {Id} - {Name}", id, request.Name);

        // Reload with products for DTO mapping
        productGroup = await _context.ProductGroups
            .Include(pg => pg.Products)
            .FirstAsync(pg => pg.Id == id, cancellationToken);

        return ProductGroupMapper.ToDto(productGroup);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting product group: {Id}", id);

        var productGroup = await _context.ProductGroups.FindAsync(new object[] { id }, cancellationToken);
        if (productGroup == null)
        {
            throw new EntityNotFoundException(nameof(ProductGroup), id);
        }

        _context.ProductGroups.Remove(productGroup);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted product group: {Id}", id);
    }

    public async Task<List<ProductSummaryDto>> GetProductsInGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var productGroup = await _context.ProductGroups.FindAsync(new object[] { groupId }, cancellationToken);
        if (productGroup == null)
        {
            throw new EntityNotFoundException(nameof(ProductGroup), groupId);
        }

        var products = await _context.Products
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Where(p => p.ProductGroupId == groupId)
            .ToListAsync(cancellationToken);

        return products.Select(ProductGroupMapper.ToProductSummaryDto).ToList();
    }
}
