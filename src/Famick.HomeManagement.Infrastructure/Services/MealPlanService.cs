using AutoMapper;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MealPlanService : IMealPlanService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAllergenWarningService _allergenWarningService;
    private readonly ILogger<MealPlanService> _logger;

    public MealPlanService(
        HomeManagementDbContext context,
        IMapper mapper,
        IAllergenWarningService allergenWarningService,
        ILogger<MealPlanService> logger)
    {
        _context = context;
        _mapper = mapper;
        _allergenWarningService = allergenWarningService;
        _logger = logger;
    }

    public async Task<MealPlanDto> GetOrCreateForWeekAsync(DateOnly weekStartDate, CancellationToken ct = default)
    {
        var plan = await GetPlanWithIncludes()
            .FirstOrDefaultAsync(mp => mp.WeekStartDate == weekStartDate, ct);

        if (plan == null)
        {
            plan = new MealPlan { WeekStartDate = weekStartDate };
            _context.MealPlans.Add(plan);
            await _context.SaveChangesAsync(ct);

            plan = await GetPlanWithIncludes()
                .FirstAsync(mp => mp.Id == plan.Id, ct);

            _logger.LogInformation("Created meal plan for week starting {WeekStartDate}", weekStartDate);
        }

        return MapToDto(plan);
    }

    public async Task<MealPlanDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await GetPlanWithIncludes()
            .FirstOrDefaultAsync(mp => mp.Id == id, ct);

        return plan == null ? null : MapToDto(plan);
    }

    public async Task<List<MealPlanSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        return await _context.MealPlans
            .Include(mp => mp.Entries)
            .OrderByDescending(mp => mp.WeekStartDate)
            .Select(mp => new MealPlanSummaryDto
            {
                Id = mp.Id,
                WeekStartDate = mp.WeekStartDate,
                EntryCount = mp.Entries.Count,
                UpdatedAt = mp.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {id} not found");

        _context.MealPlans.Remove(plan);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted meal plan {MealPlanId}", id);
    }

    public async Task<MealPlanEntryDto> AddEntryAsync(
        Guid planId, CreateMealPlanEntryRequest request, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans
            .Include(mp => mp.Entries)
            .FirstOrDefaultAsync(mp => mp.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var entry = _mapper.Map<MealPlanEntry>(request);
        entry.MealPlanId = planId;
        plan.UpdatedByUserId = userId;

        plan.Entries.Add(entry);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Added entry {EntryId} to meal plan {MealPlanId}", entry.Id, planId);
        return await ReloadEntryAsync(entry.Id, ct);
    }

    public async Task<MealPlanEntryDto> UpdateEntryAsync(
        Guid planId, Guid entryId, UpdateMealPlanEntryRequest request, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var entry = await _context.MealPlanEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        _mapper.Map(request, entry);
        plan.UpdatedByUserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Updated entry {EntryId} in meal plan {MealPlanId}", entryId, planId);
        return await ReloadEntryAsync(entryId, ct);
    }

    public async Task DeleteEntryAsync(
        Guid planId, Guid entryId, uint expectedVersion, Guid userId,
        string? batchAction = null, CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var entry = await _context.MealPlanEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        if (entry.IsBatchSource)
        {
            var dependents = await _context.MealPlanEntries
                .Where(e => e.BatchSourceEntryId == entryId)
                .ToListAsync(ct);

            if (dependents.Count > 0)
            {
                if (batchAction == null)
                    throw new BatchSourceHasDependentsException(dependents.Count);

                if (batchAction == "convert")
                {
                    foreach (var dep in dependents)
                        dep.BatchSourceEntryId = null;
                }
                else if (batchAction == "cascade")
                {
                    _context.MealPlanEntries.RemoveRange(dependents);
                }
            }
        }

        plan.UpdatedByUserId = userId;
        _context.MealPlanEntries.Remove(entry);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Deleted entry {EntryId} from meal plan {MealPlanId} (batchAction={BatchAction})", entryId, planId, batchAction);
    }

    public async Task<ShoppingListPreviewDto> GenerateShoppingListAsync(
        Guid planId, GenerateShoppingListRequest request, CancellationToken ct = default)
    {
        var plan = await _context.MealPlans
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Product)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.ProductQuantityUnit)
            .FirstOrDefaultAsync(mp => mp.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        var preview = new ShoppingListPreviewDto();

        // Get eligible entries (exclude "from batch" entries and inline notes)
        var eligibleEntries = plan.Entries
            .Where(e => e.MealId.HasValue && !e.BatchSourceEntryId.HasValue)
            .ToList();

        foreach (var entry in eligibleEntries)
        {
            if (entry.Meal == null) continue;

            foreach (var item in entry.Meal.Items)
            {
                if (item.ItemType == Domain.Enums.MealItemType.Product && item.ProductId.HasValue && item.Product != null)
                {
                    var previewItem = new ShoppingListPreviewItemDto
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        Quantity = item.ProductQuantity ?? 1,
                        QuantityUnitName = item.ProductQuantityUnit?.Name
                    };

                    // Check stock
                    var stock = await _context.Stock
                        .Where(s => s.ProductId == item.ProductId)
                        .SumAsync(s => s.Amount, ct);

                    previewItem.CurrentStock = stock;

                    if (stock >= (item.ProductQuantity ?? 1))
                        preview.InStockItems.Add(previewItem);
                    else
                        preview.NeededItems.Add(previewItem);
                }
                else if (item.ItemType == Domain.Enums.MealItemType.Freetext && !string.IsNullOrEmpty(item.FreetextDescription))
                {
                    preview.UntrackedItems.Add(item.FreetextDescription);
                }
                // Recipe items would need ingredient-level breakdown
            }
        }

        return preview;
    }

    public async Task<MealPlanNutritionDto> GetNutritionAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await GetPlanWithIncludes()
            .FirstOrDefaultAsync(mp => mp.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        var result = new MealPlanNutritionDto
        {
            MealPlanId = planId,
            WeekStartDate = plan.WeekStartDate
        };

        for (int day = 0; day <= 6; day++)
        {
            var dailyNutrition = new DailyNutritionDto { DayOfWeek = day };
            var dayEntries = plan.Entries.Where(e => e.DayOfWeek == day && e.Meal != null);

            foreach (var entry in dayEntries)
            {
                foreach (var item in entry.Meal!.Items)
                {
                    if (item.Product?.Nutrition != null)
                    {
                        var n = item.Product.Nutrition;
                        var qty = item.ProductQuantity ?? 1;
                        dailyNutrition.Calories += (n.Calories ?? 0) * qty;
                        dailyNutrition.ProteinGrams += (n.Protein ?? 0) * qty;
                        dailyNutrition.CarbsGrams += (n.TotalCarbohydrates ?? 0) * qty;
                        dailyNutrition.FatGrams += (n.TotalFat ?? 0) * qty;
                    }
                }
            }

            result.DailyBreakdown.Add(dailyNutrition);
            result.WeeklyCalories += dailyNutrition.Calories;
            result.WeeklyProteinGrams += dailyNutrition.ProteinGrams;
            result.WeeklyCarbsGrams += dailyNutrition.CarbsGrams;
            result.WeeklyFatGrams += dailyNutrition.FatGrams;
        }

        return result;
    }

    public async Task<TodaysMealsDto> GetTodaysMealsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayOfWeek = ((int)today.DayOfWeek + 6) % 7; // Convert to Monday=0
        var weekStart = today.AddDays(-dayOfWeek);

        var plan = await GetPlanWithIncludes()
            .FirstOrDefaultAsync(mp => mp.WeekStartDate == weekStart, ct);

        var result = new TodaysMealsDto { Date = today };

        if (plan == null)
            return result;

        var todaysEntries = plan.Entries
            .Where(e => e.DayOfWeek == dayOfWeek)
            .GroupBy(e => e.MealType);

        foreach (var group in todaysEntries.OrderBy(g => g.Key.SortOrder))
        {
            var mealGroup = new TodaysMealGroupDto
            {
                MealTypeId = group.Key.Id,
                MealTypeName = group.Key.Name,
                MealTypeColor = group.Key.Color,
                Entries = group.OrderBy(e => e.SortOrder).Select(e => new MealPlanEntryDto
                {
                    Id = e.Id,
                    MealId = e.MealId,
                    MealName = e.Meal?.Name,
                    InlineNote = e.InlineNote,
                    MealTypeId = e.MealTypeId,
                    MealTypeName = e.MealType.Name,
                    DayOfWeek = e.DayOfWeek,
                    SortOrder = e.SortOrder,
                    IsBatchSource = e.IsBatchSource,
                    BatchSourceEntryId = e.BatchSourceEntryId
                }).ToList()
            };

            result.MealGroups.Add(mealGroup);
        }

        return result;
    }

    public async Task<MealPlanAllergenWarningsDto> GetAllergenWarningsAsync(Guid planId, CancellationToken ct = default)
    {
        return await _allergenWarningService.CheckMealPlanAsync(planId, ct);
    }

    #region Private Helpers

    private IQueryable<MealPlan> GetPlanWithIncludes()
    {
        return _context.MealPlans
            .Include(mp => mp.UpdatedByUser)
            .Include(mp => mp.Entries.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder))
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items.OrderBy(i => i.SortOrder))
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.Nutrition)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.MealType);
    }

    private MealPlanDto MapToDto(MealPlan plan)
    {
        return new MealPlanDto
        {
            Id = plan.Id,
            WeekStartDate = plan.WeekStartDate,
            UpdatedByUserId = plan.UpdatedByUserId,
            UpdatedByUserName = plan.UpdatedByUser != null ? $"{plan.UpdatedByUser.FirstName} {plan.UpdatedByUser.LastName}".Trim() : null,
            Version = plan.Version,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            Entries = plan.Entries.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => new MealPlanEntryDto
            {
                Id = e.Id,
                MealId = e.MealId,
                MealName = e.Meal?.Name,
                InlineNote = e.InlineNote,
                MealTypeId = e.MealTypeId,
                MealTypeName = e.MealType.Name,
                DayOfWeek = e.DayOfWeek,
                SortOrder = e.SortOrder,
                IsBatchSource = e.IsBatchSource,
                BatchSourceEntryId = e.BatchSourceEntryId
            }).ToList()
        };
    }

    private async Task<MealPlanEntryDto> ReloadEntryAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await _context.MealPlanEntries
            .Include(e => e.Meal)
            .Include(e => e.MealType)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new KeyNotFoundException($"Entry {entryId} not found");

        return new MealPlanEntryDto
        {
            Id = entry.Id,
            MealId = entry.MealId,
            MealName = entry.Meal?.Name,
            InlineNote = entry.InlineNote,
            MealTypeId = entry.MealTypeId,
            MealTypeName = entry.MealType.Name,
            DayOfWeek = entry.DayOfWeek,
            SortOrder = entry.SortOrder,
            IsBatchSource = entry.IsBatchSource,
            BatchSourceEntryId = entry.BatchSourceEntryId
        };
    }

    private static void VerifyVersion(MealPlan plan, uint expectedVersion)
    {
        if (plan.Version != expectedVersion)
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
    }

    #endregion
}

/// <summary>
/// Thrown when a concurrent edit is detected on a meal plan.
/// </summary>
public class MealPlanConcurrencyException : Exception
{
    public Guid? UpdatedByUserId { get; }

    public MealPlanConcurrencyException(Guid? updatedByUserId)
        : base("The meal plan was modified by another user")
    {
        UpdatedByUserId = updatedByUserId;
    }
}

/// <summary>
/// Thrown when attempting to delete a batch source entry that has linked dependents
/// without specifying a batch action (convert or cascade).
/// </summary>
public class BatchSourceHasDependentsException : Exception
{
    public int DependentCount { get; }

    public BatchSourceHasDependentsException(int dependentCount)
        : base($"This batch source has {dependentCount} linked entries") =>
        DependentCount = dependentCount;
}
