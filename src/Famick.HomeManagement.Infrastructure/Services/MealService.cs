using AutoMapper;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MealService : IMealService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAllergenWarningService _allergenWarningService;
    private readonly ILogger<MealService> _logger;

    public MealService(
        HomeManagementDbContext context,
        IMapper mapper,
        IAllergenWarningService allergenWarningService,
        ILogger<MealService> logger)
    {
        _context = context;
        _mapper = mapper;
        _allergenWarningService = allergenWarningService;
        _logger = logger;
    }

    public async Task<MealDto> CreateAsync(CreateMealRequest request, CancellationToken ct = default)
    {
        var meal = _mapper.Map<Meal>(request);

        foreach (var itemRequest in request.Items)
        {
            var item = _mapper.Map<MealItem>(itemRequest);
            meal.Items.Add(item);
        }

        _context.Meals.Add(meal);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created meal {MealId} '{Name}'", meal.Id, meal.Name);
        return await ReloadAndMapAsync(meal.Id, ct);
    }

    public async Task<MealDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var meal = await GetMealWithIncludes()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return meal == null ? null : MapToDto(meal);
    }

    public async Task<List<MealSummaryDto>> ListAsync(MealFilterRequest? filter = null, CancellationToken ct = default)
    {
        var query = _context.Meals
            .Include(m => m.Items)
            .AsQueryable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(m => m.Name.ToLower().Contains(term));
            }

            if (filter.IsFavorite.HasValue)
            {
                query = query.Where(m => m.IsFavorite == filter.IsFavorite.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "createdat" => filter.Descending ? query.OrderByDescending(m => m.CreatedAt) : query.OrderBy(m => m.CreatedAt),
                "updatedat" => filter.Descending ? query.OrderByDescending(m => m.UpdatedAt) : query.OrderBy(m => m.UpdatedAt),
                _ => filter.Descending ? query.OrderByDescending(m => m.Name) : query.OrderBy(m => m.Name),
            };
        }
        else
        {
            query = query.OrderBy(m => m.Name);
        }

        var meals = await query.ToListAsync(ct);
        return meals.Select(m => new MealSummaryDto
        {
            Id = m.Id,
            Name = m.Name,
            IsFavorite = m.IsFavorite,
            ItemCount = m.Items.Count,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task<MealDto> UpdateAsync(Guid id, UpdateMealRequest request, CancellationToken ct = default)
    {
        var meal = await _context.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException($"Meal with ID {id} not found");

        _mapper.Map(request, meal);

        // Replace items entirely
        _context.MealItems.RemoveRange(meal.Items);
        meal.Items.Clear();

        foreach (var itemRequest in request.Items)
        {
            var item = _mapper.Map<MealItem>(itemRequest);
            item.MealId = id;
            meal.Items.Add(item);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated meal {MealId}", id);
        return await ReloadAndMapAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var meal = await _context.Meals.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Meal with ID {id} not found");

        // Check if referenced by current or future meal plan entries
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentWeekStart = today.AddDays(-(int)today.DayOfWeek + (int)System.DayOfWeek.Monday);
        if (today.DayOfWeek == System.DayOfWeek.Sunday) currentWeekStart = currentWeekStart.AddDays(-7);

        var isReferenced = await _context.MealPlanEntries
            .AnyAsync(e => e.MealId == id && e.MealPlan.WeekStartDate >= currentWeekStart, ct);

        if (isReferenced)
            throw new InvalidOperationException("Cannot delete a meal that is referenced by current or future meal plans");

        _context.Meals.Remove(meal);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted meal {MealId}", id);
    }

    public async Task<MealNutritionDto> GetNutritionAsync(Guid id, CancellationToken ct = default)
    {
        var meal = await GetMealWithIncludes()
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException($"Meal with ID {id} not found");

        var result = new MealNutritionDto { MealId = id };

        foreach (var item in meal.Items)
        {
            var itemNutrition = new MealItemNutritionDto();

            if (item.ItemType == MealItemType.Product && item.Product?.Nutrition != null)
            {
                var n = item.Product.Nutrition;
                var quantity = item.ProductQuantity ?? 1;
                itemNutrition.ItemName = item.Product.Name;
                itemNutrition.ItemId = item.Id;
                itemNutrition.Calories = (n.Calories ?? 0) * quantity;
                itemNutrition.ProteinGrams = (n.Protein ?? 0) * quantity;
                itemNutrition.CarbsGrams = (n.TotalCarbohydrates ?? 0) * quantity;
                itemNutrition.FatGrams = (n.TotalFat ?? 0) * quantity;
                itemNutrition.HasNutritionData = true;
            }
            else if (item.ItemType == MealItemType.Recipe && item.Recipe != null)
            {
                itemNutrition.ItemName = item.Recipe.Name;
                itemNutrition.ItemId = item.Id;
                // Recipe nutrition would need aggregation from ingredients - simplified here
                itemNutrition.HasNutritionData = false;
            }
            else
            {
                itemNutrition.ItemName = item.FreetextDescription ?? "Unknown";
                itemNutrition.ItemId = item.Id;
                itemNutrition.HasNutritionData = false;
            }

            result.ItemNutrition.Add(itemNutrition);
            result.TotalCalories += itemNutrition.Calories;
            result.TotalProteinGrams += itemNutrition.ProteinGrams;
            result.TotalCarbsGrams += itemNutrition.CarbsGrams;
            result.TotalFatGrams += itemNutrition.FatGrams;
        }

        return result;
    }

    public async Task<MealSuggestionDto> GetSuggestionsAsync(CancellationToken ct = default)
    {
        var result = new MealSuggestionDto();

        // Favorites
        result.Favorites = await _context.Meals
            .Include(m => m.Items)
            .Where(m => m.IsFavorite)
            .OrderBy(m => m.Name)
            .Take(10)
            .Select(m => new MealSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                IsFavorite = m.IsFavorite,
                ItemCount = m.Items.Count,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        // Recent (used in last 4 weeks)
        var fourWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));
        var recentMealIds = await _context.MealPlanEntries
            .Where(e => e.MealId.HasValue && e.MealPlan.WeekStartDate >= fourWeeksAgo)
            .Select(e => e.MealId!.Value)
            .Distinct()
            .ToListAsync(ct);

        result.Recent = await _context.Meals
            .Include(m => m.Items)
            .Where(m => recentMealIds.Contains(m.Id))
            .OrderByDescending(m => m.UpdatedAt)
            .Take(10)
            .Select(m => new MealSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                IsFavorite = m.IsFavorite,
                ItemCount = m.Items.Count,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return result;
    }

    public async Task<AllergenCheckResultDto> CheckAllergensAsync(Guid id, CancellationToken ct = default)
    {
        return await _allergenWarningService.CheckMealAsync(id, ct);
    }

    #region Private Helpers

    private IQueryable<Meal> GetMealWithIncludes()
    {
        return _context.Meals
            .Include(m => m.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Recipe)
            .Include(m => m.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.Nutrition)
            .Include(m => m.Items)
                .ThenInclude(i => i.ProductQuantityUnit);
    }

    private MealDto MapToDto(Meal meal)
    {
        return new MealDto
        {
            Id = meal.Id,
            Name = meal.Name,
            Notes = meal.Notes,
            IsFavorite = meal.IsFavorite,
            CreatedAt = meal.CreatedAt,
            UpdatedAt = meal.UpdatedAt,
            Items = meal.Items.OrderBy(i => i.SortOrder).Select(i => new MealItemDto
            {
                Id = i.Id,
                ItemType = i.ItemType,
                RecipeId = i.RecipeId,
                RecipeName = i.Recipe?.Name,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name,
                ProductQuantity = i.ProductQuantity,
                ProductQuantityUnitId = i.ProductQuantityUnitId,
                ProductQuantityUnitName = i.ProductQuantityUnit?.Name,
                FreetextDescription = i.FreetextDescription,
                SortOrder = i.SortOrder
            }).ToList()
        };
    }

    private async Task<MealDto> ReloadAndMapAsync(Guid id, CancellationToken ct)
    {
        var meal = await GetMealWithIncludes()
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException($"Meal with ID {id} not found");

        return MapToDto(meal);
    }

    #endregion
}
