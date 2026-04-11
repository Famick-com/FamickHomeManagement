using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MealTypeService : IMealTypeService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<MealTypeService> _logger;

    private const int MaxMealTypesPerTenant = 10;

    public MealTypeService(
        HomeManagementDbContext context,
        ILogger<MealTypeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<MealTypeDto>> ListAsync(CancellationToken ct = default)
    {
        var mealTypes = await _context.MealTypes
            .OrderBy(mt => mt.SortOrder)
            .ThenBy(mt => mt.Name)
            .ToListAsync(ct);

        return mealTypes.Select(MealPlannerMapper.ToMealTypeDto).ToList();
    }

    public async Task<MealTypeDto> CreateAsync(CreateMealTypeRequest request, CancellationToken ct = default)
    {
        var currentCount = await _context.MealTypes.CountAsync(ct);
        if (currentCount >= MaxMealTypesPerTenant)
            throw new InvalidOperationException($"Maximum of {MaxMealTypesPerTenant} meal types per tenant reached");

        var existingName = await _context.MealTypes
            .AnyAsync(mt => mt.Name.ToLower() == request.Name.ToLower(), ct);
        if (existingName)
            throw new InvalidOperationException($"A meal type with the name '{request.Name}' already exists");

        var mealType = MealPlannerMapper.FromCreateMealTypeRequest(request);
        _context.MealTypes.Add(mealType);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created meal type {MealTypeId} '{Name}'", mealType.Id, mealType.Name);
        return MealPlannerMapper.ToMealTypeDto(mealType);
    }

    public async Task<MealTypeDto> UpdateAsync(Guid id, UpdateMealTypeRequest request, CancellationToken ct = default)
    {
        var mealType = await _context.MealTypes.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Meal type with ID {id} not found");

        var duplicateName = await _context.MealTypes
            .AnyAsync(mt => mt.Id != id && mt.Name.ToLower() == request.Name.ToLower(), ct);
        if (duplicateName)
            throw new InvalidOperationException($"A meal type with the name '{request.Name}' already exists");

        MealPlannerMapper.UpdateMealType(request, mealType);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated meal type {MealTypeId}", id);
        return MealPlannerMapper.ToMealTypeDto(mealType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var mealType = await _context.MealTypes.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Meal type with ID {id} not found");

        if (mealType.IsDefault)
            throw new InvalidOperationException("Default meal types cannot be deleted");

        var isReferenced = await _context.MealPlanEntries
            .AnyAsync(e => e.MealTypeId == id, ct);
        if (isReferenced)
            throw new InvalidOperationException("Cannot delete a meal type that is referenced by meal plan entries");

        _context.MealTypes.Remove(mealType);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted meal type {MealTypeId}", id);
    }

    public async Task SeedDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var hasAny = await _context.MealTypes.AnyAsync(ct);
        if (hasAny)
            return;

        var defaults = new[]
        {
            new MealType { Name = "Breakfast", SortOrder = 0, IsDefault = true, Color = "#FFA726", TenantId = tenantId },
            new MealType { Name = "Lunch", SortOrder = 1, IsDefault = true, Color = "#66BB6A", TenantId = tenantId },
            new MealType { Name = "Dinner", SortOrder = 2, IsDefault = true, Color = "#42A5F5", TenantId = tenantId },
            new MealType { Name = "Snack", SortOrder = 3, IsDefault = true, Color = "#AB47BC", TenantId = tenantId }
        };

        _context.MealTypes.AddRange(defaults);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded default meal types for tenant {TenantId}", tenantId);
    }

    public async Task CreateFromOnboardingAsync(Guid tenantId, List<OnboardingMealTypeSelection> selections, CancellationToken ct = default)
    {
        var hasAny = await _context.MealTypes.AnyAsync(ct);
        if (hasAny)
            return;

        var toCreate = selections.Take(MaxMealTypesPerTenant).Select((s, i) => new MealType
        {
            Name = s.Name,
            Color = s.Color,
            SortOrder = i,
            IsDefault = true,
            TenantId = tenantId
        }).ToList();

        _context.MealTypes.AddRange(toCreate);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created {Count} meal types from onboarding for tenant {TenantId}", toCreate.Count, tenantId);
    }
}
