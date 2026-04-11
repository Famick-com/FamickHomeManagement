using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MealPlanService : IMealPlanService
{
    private readonly HomeManagementDbContext _context;
    private readonly IAllergenWarningService _allergenWarningService;
    private readonly ILogger<MealPlanService> _logger;

    public MealPlanService(
        HomeManagementDbContext context,
        IAllergenWarningService allergenWarningService,
        ILogger<MealPlanService> logger)
    {
        _context = context;
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

        var entry = MealPlannerMapper.FromCreateMealPlanEntryRequest(request);
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

        MealPlannerMapper.UpdateMealPlanEntry(request, entry);
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
            var wholeMealDependents = await _context.MealPlanEntries
                .Where(e => e.BatchSourceEntryId == entryId)
                .ToListAsync(ct);

            var ingredientUsageCount = await _context.BatchCookItemUsages
                .CountAsync(u => u.BatchCookItem.SourceEntryId == entryId, ct);

            var totalDependentCount = wholeMealDependents.Count + ingredientUsageCount;

            if (totalDependentCount > 0)
            {
                if (batchAction == null)
                    throw new BatchSourceHasDependentsException(totalDependentCount);

                if (batchAction == "convert")
                {
                    foreach (var dep in wholeMealDependents)
                        dep.BatchSourceEntryId = null;

                    // Remove ingredient-level usages (keep batch cook items, they'll cascade on entry delete)
                    var usages = await _context.BatchCookItemUsages
                        .Where(u => u.BatchCookItem.SourceEntryId == entryId)
                        .ToListAsync(ct);
                    _context.BatchCookItemUsages.RemoveRange(usages);
                }
                else if (batchAction == "cascade")
                {
                    _context.MealPlanEntries.RemoveRange(wholeMealDependents);
                    // BatchCookItems and their Usages cascade-delete via DB FK
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
        // Load plan with entries, meals, items, products, batch cook data
        var plan = await _context.MealPlans
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Product)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.ProductQuantityUnit)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Recipe)
                            .ThenInclude(r => r!.Steps)
                                .ThenInclude(s => s.Ingredients)
                                    .ThenInclude(p => p.Product)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Recipe)
                            .ThenInclude(r => r!.Steps)
                                .ThenInclude(s => s.Ingredients)
                                    .ThenInclude(p => p.QuantityUnit)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItems)
                    .ThenInclude(bci => bci.Usages)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItems)
                    .ThenInclude(bci => bci.Product)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItemUsages)
                    .ThenInclude(u => u.BatchCookItem)
                        .ThenInclude(bci => bci.Product)
            .FirstOrDefaultAsync(mp => mp.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        var preview = new ShoppingListPreviewDto();

        // 1. Calculate total demand from ALL entries with meals (including from-batch entries)
        var demand = new Dictionary<Guid, (decimal Quantity, string ProductName, string? UnitName)>();
        var untrackedItems = new HashSet<string>();

        foreach (var entry in plan.Entries.Where(e => e.MealId.HasValue && e.Meal != null))
        {
            foreach (var item in entry.Meal!.Items)
            {
                if (item.ItemType == Domain.Enums.MealItemType.Product && item.ProductId.HasValue && item.Product != null)
                {
                    var qty = item.ProductQuantity ?? 1;
                    if (demand.TryGetValue(item.ProductId.Value, out var existing))
                        demand[item.ProductId.Value] = (existing.Quantity + qty, existing.ProductName, existing.UnitName);
                    else
                        demand[item.ProductId.Value] = (qty, item.Product.Name, item.ProductQuantityUnit?.Name);
                }
                else if (item.ItemType == Domain.Enums.MealItemType.Recipe && item.Recipe != null)
                {
                    // Resolve recipe ingredients via RecipeStep -> RecipePosition (Ingredients)
                    foreach (var step in item.Recipe.Steps)
                    {
                        foreach (var ingredient in step.Ingredients)
                        {
                            if (ingredient.Product != null)
                            {
                                var qty = ingredient.Amount;
                                if (demand.TryGetValue(ingredient.ProductId, out var existing))
                                    demand[ingredient.ProductId] = (existing.Quantity + qty, existing.ProductName, existing.UnitName);
                                else
                                    demand[ingredient.ProductId] = (qty, ingredient.Product.Name, ingredient.QuantityUnit?.Name);
                            }
                        }
                    }
                }
                else if (item.ItemType == Domain.Enums.MealItemType.Freetext && !string.IsNullOrEmpty(item.FreetextDescription))
                {
                    untrackedItems.Add(item.FreetextDescription);
                }
            }
        }

        // 2. Calculate batch coverage
        var coverage = new Dictionary<Guid, (decimal Quantity, string SourceDescription)>();

        // 2a. OLD whole-meal links (BatchSourceEntryId): overlapping products deducted
        foreach (var depEntry in plan.Entries.Where(e => e.BatchSourceEntryId.HasValue && e.Meal != null))
        {
            var sourceEntry = plan.Entries.FirstOrDefault(e => e.Id == depEntry.BatchSourceEntryId);
            if (sourceEntry?.Meal == null) continue;

            var sourceProductIds = sourceEntry.Meal.Items
                .Where(i => i.ItemType == Domain.Enums.MealItemType.Product && i.ProductId.HasValue)
                .Select(i => i.ProductId!.Value)
                .ToHashSet();

            foreach (var item in depEntry.Meal!.Items)
            {
                if (item.ItemType == Domain.Enums.MealItemType.Product && item.ProductId.HasValue && sourceProductIds.Contains(item.ProductId.Value))
                {
                    var qty = item.ProductQuantity ?? 1;
                    var dayName = GetDayName(sourceEntry.DayOfWeek);
                    var desc = $"{dayName}'s {sourceEntry.Meal.Name}";

                    if (coverage.TryGetValue(item.ProductId.Value, out var existing))
                        coverage[item.ProductId.Value] = (existing.Quantity + qty, existing.SourceDescription);
                    else
                        coverage[item.ProductId.Value] = (qty, desc);
                }
            }
        }

        // 2b. NEW ingredient-level links (BatchCookItemUsage)
        foreach (var entry in plan.Entries)
        {
            foreach (var usage in entry.BatchCookItemUsages)
            {
                var bci = usage.BatchCookItem;
                if (bci?.Product == null) continue;

                // Determine quantity: explicit or auto from meal's product quantity
                decimal qty;
                if (usage.QuantityUsed.HasValue)
                {
                    qty = usage.QuantityUsed.Value;
                }
                else
                {
                    // Auto: find the product in the dependent entry's meal
                    qty = entry.Meal?.Items
                        .Where(i => i.ProductId == bci.ProductId && i.ItemType == Domain.Enums.MealItemType.Product)
                        .Sum(i => i.ProductQuantity ?? 1) ?? 0;
                }

                if (qty <= 0) continue;

                var sourceEntry = plan.Entries.FirstOrDefault(e => e.Id == bci.SourceEntryId);
                var dayName = GetDayName(sourceEntry?.DayOfWeek ?? 0);
                var desc = $"{dayName}'s {sourceEntry?.Meal?.Name ?? "batch"}";

                if (coverage.TryGetValue(bci.ProductId, out var existing))
                    coverage[bci.ProductId] = (existing.Quantity + qty, existing.SourceDescription);
                else
                    coverage[bci.ProductId] = (qty, desc);
            }
        }

        // 3. Effective demand = demand - coverage (floor at 0)
        foreach (var (productId, (totalQty, productName, unitName)) in demand)
        {
            var coveredQty = coverage.TryGetValue(productId, out var cov) ? cov.Quantity : 0m;
            var effectiveQty = Math.Max(0, totalQty - coveredQty);

            if (effectiveQty <= 0)
            {
                // Fully covered by batch
                preview.BatchCoveredItems.Add(new ShoppingListPreviewItemDto
                {
                    ProductId = productId,
                    ProductName = productName,
                    Quantity = totalQty,
                    QuantityUnitName = unitName,
                    BatchCoveredQuantity = coveredQty,
                    BatchSourceDescription = coverage.TryGetValue(productId, out var c) ? c.SourceDescription : null
                });
                continue;
            }

            // Check stock
            var stock = await _context.Stock
                .Where(s => s.ProductId == productId)
                .SumAsync(s => s.Amount, ct);

            var previewItem = new ShoppingListPreviewItemDto
            {
                ProductId = productId,
                ProductName = productName,
                Quantity = effectiveQty,
                QuantityUnitName = unitName,
                CurrentStock = stock,
                BatchCoveredQuantity = coveredQty > 0 ? coveredQty : null,
                BatchSourceDescription = coveredQty > 0 && coverage.TryGetValue(productId, out var cd) ? cd.SourceDescription : null
            };

            if (stock >= effectiveQty)
                preview.InStockItems.Add(previewItem);
            else
                preview.NeededItems.Add(previewItem);
        }

        preview.UntrackedItems = untrackedItems.ToList();

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

    #region Ingredient-Level Batch Cooking

    public async Task<BatchCookItemDto> AddBatchCookItemAsync(
        Guid planId, Guid entryId, CreateBatchCookItemRequest request, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var entry = await _context.MealPlanEntries
            .Include(e => e.Meal)
                .ThenInclude(m => m!.Items)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        if (entry.Meal == null)
            throw new InvalidOperationException("Cannot add batch cook items to an inline note entry");

        // Validate product is in the meal (direct product items)
        var mealProductIds = entry.Meal.Items
            .Where(i => i.ItemType == Domain.Enums.MealItemType.Product && i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .ToHashSet();

        // Also check recipe ingredients
        if (!mealProductIds.Contains(request.ProductId))
        {
            var recipeItems = entry.Meal.Items
                .Where(i => i.ItemType == Domain.Enums.MealItemType.Recipe && i.RecipeId.HasValue)
                .Select(i => i.RecipeId!.Value)
                .ToList();

            if (recipeItems.Count > 0)
            {
                var recipeProductIds = await _context.Set<RecipePosition>()
                    .Where(rp => recipeItems.Contains(rp.RecipeStep.RecipeId))
                    .Select(rp => rp.ProductId)
                    .ToListAsync(ct);

                mealProductIds.UnionWith(recipeProductIds);
            }
        }

        if (!mealProductIds.Contains(request.ProductId))
            throw new InvalidOperationException("Product is not an ingredient in this meal");

        // Check for duplicate
        var exists = await _context.BatchCookItems
            .AnyAsync(bci => bci.SourceEntryId == entryId && bci.ProductId == request.ProductId, ct);
        if (exists)
            throw new InvalidOperationException("This product is already marked as batch-cooked for this entry");

        var batchItem = new BatchCookItem
        {
            SourceEntryId = entryId,
            ProductId = request.ProductId,
            TotalQuantity = request.TotalQuantity,
            QuantityUnitId = request.QuantityUnitId
        };

        _context.BatchCookItems.Add(batchItem);

        // Auto-set IsBatchSource on the entry
        if (!entry.IsBatchSource)
        {
            entry.IsBatchSource = true;
        }

        plan.UpdatedByUserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Added batch cook item {BatchCookItemId} for product {ProductId} on entry {EntryId}", batchItem.Id, request.ProductId, entryId);
        return await ReloadBatchCookItemAsync(batchItem.Id, ct);
    }

    public async Task RemoveBatchCookItemAsync(
        Guid planId, Guid entryId, Guid batchCookItemId, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var batchItem = await _context.BatchCookItems
            .FirstOrDefaultAsync(bci => bci.Id == batchCookItemId && bci.SourceEntryId == entryId, ct)
            ?? throw new KeyNotFoundException($"Batch cook item with ID {batchCookItemId} not found");

        // Verify entry belongs to plan
        var entry = await _context.MealPlanEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        _context.BatchCookItems.Remove(batchItem); // Usages cascade-delete

        plan.UpdatedByUserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Removed batch cook item {BatchCookItemId} from entry {EntryId}", batchCookItemId, entryId);
    }

    public async Task<List<BatchCookItemDto>> GetBatchCookItemsAsync(Guid planId, Guid entryId, CancellationToken ct = default)
    {
        // Verify entry belongs to plan
        var entryExists = await _context.MealPlanEntries
            .AnyAsync(e => e.Id == entryId && e.MealPlanId == planId, ct);
        if (!entryExists)
            throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        var items = await _context.BatchCookItems
            .Include(bci => bci.Product)
            .Include(bci => bci.QuantityUnit)
            .Include(bci => bci.Usages)
                .ThenInclude(u => u.DependentEntry)
                    .ThenInclude(e => e.Meal)
            .Where(bci => bci.SourceEntryId == entryId)
            .ToListAsync(ct);

        return items.Select(MapBatchCookItemToDto).ToList();
    }

    public async Task<BatchCookItemUsageDto> LinkBatchCookItemAsync(
        Guid planId, Guid entryId, LinkBatchCookItemRequest request, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        // Verify the dependent entry belongs to this plan
        var entry = await _context.MealPlanEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        // Verify batch cook item exists and belongs to the same plan
        var batchItem = await _context.BatchCookItems
            .Include(bci => bci.SourceEntry)
            .FirstOrDefaultAsync(bci => bci.Id == request.BatchCookItemId, ct)
            ?? throw new KeyNotFoundException($"Batch cook item with ID {request.BatchCookItemId} not found");

        if (batchItem.SourceEntry.MealPlanId != planId)
            throw new InvalidOperationException("Batch cook item does not belong to this meal plan");

        if (batchItem.SourceEntryId == entryId)
            throw new InvalidOperationException("Cannot link a batch cook item to its own source entry");

        // Check for duplicate
        var exists = await _context.BatchCookItemUsages
            .AnyAsync(u => u.BatchCookItemId == request.BatchCookItemId && u.DependentEntryId == entryId, ct);
        if (exists)
            throw new InvalidOperationException("This batch cook item is already linked to this entry");

        var usage = new BatchCookItemUsage
        {
            BatchCookItemId = request.BatchCookItemId,
            DependentEntryId = entryId,
            QuantityUsed = request.QuantityUsed
        };

        _context.BatchCookItemUsages.Add(usage);
        plan.UpdatedByUserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Linked batch cook item {BatchCookItemId} to entry {EntryId}", request.BatchCookItemId, entryId);

        // Reload with navigation
        var saved = await _context.BatchCookItemUsages
            .Include(u => u.DependentEntry)
                .ThenInclude(e => e.Meal)
            .FirstAsync(u => u.Id == usage.Id, ct);

        return new BatchCookItemUsageDto
        {
            Id = saved.Id,
            BatchCookItemId = saved.BatchCookItemId,
            DependentEntryId = saved.DependentEntryId,
            DependentEntryMealName = saved.DependentEntry.Meal?.Name,
            DependentEntryDayOfWeek = saved.DependentEntry.DayOfWeek,
            QuantityUsed = saved.QuantityUsed
        };
    }

    public async Task UnlinkBatchCookItemAsync(
        Guid planId, Guid entryId, Guid usageId, uint expectedVersion, Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _context.MealPlans.FindAsync([planId], ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {planId} not found");

        VerifyVersion(plan, expectedVersion);

        var usage = await _context.BatchCookItemUsages
            .FirstOrDefaultAsync(u => u.Id == usageId && u.DependentEntryId == entryId, ct)
            ?? throw new KeyNotFoundException($"Batch cook item usage with ID {usageId} not found");

        // Verify entry belongs to plan
        var entryExists = await _context.MealPlanEntries
            .AnyAsync(e => e.Id == entryId && e.MealPlanId == planId, ct);
        if (!entryExists)
            throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        _context.BatchCookItemUsages.Remove(usage);
        plan.UpdatedByUserId = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
        }

        _logger.LogInformation("Unlinked batch cook item usage {UsageId} from entry {EntryId}", usageId, entryId);
    }

    public async Task<List<BatchCookSuggestionDto>> GetBatchCookSuggestionsAsync(Guid planId, Guid entryId, CancellationToken ct = default)
    {
        // Load the entry's meal items to get product IDs
        var entry = await _context.MealPlanEntries
            .Include(e => e.Meal)
                .ThenInclude(m => m!.Items)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == planId, ct)
            ?? throw new KeyNotFoundException($"Meal plan entry with ID {entryId} not found");

        if (entry.Meal == null)
            return new List<BatchCookSuggestionDto>();

        // Collect all product IDs from this entry's meal (direct products)
        var entryProductIds = entry.Meal.Items
            .Where(i => i.ItemType == Domain.Enums.MealItemType.Product && i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .ToHashSet();

        // Also include recipe ingredients
        var recipeIds = entry.Meal.Items
            .Where(i => i.ItemType == Domain.Enums.MealItemType.Recipe && i.RecipeId.HasValue)
            .Select(i => i.RecipeId!.Value)
            .ToList();

        if (recipeIds.Count > 0)
        {
            var recipeProductIds = await _context.Set<RecipePosition>()
                .Where(rp => recipeIds.Contains(rp.RecipeStep.RecipeId))
                .Select(rp => rp.ProductId)
                .ToListAsync(ct);
            entryProductIds.UnionWith(recipeProductIds);
        }

        if (entryProductIds.Count == 0)
            return new List<BatchCookSuggestionDto>();

        // Get IDs of batch cook items already linked to this entry
        var alreadyLinkedItemIds = await _context.BatchCookItemUsages
            .Where(u => u.DependentEntryId == entryId)
            .Select(u => u.BatchCookItemId)
            .ToListAsync(ct);

        // Find matching batch cook items in the same plan
        var suggestions = await _context.BatchCookItems
            .Include(bci => bci.Product)
            .Include(bci => bci.QuantityUnit)
            .Include(bci => bci.SourceEntry)
                .ThenInclude(e => e.Meal)
            .Include(bci => bci.Usages)
            .Where(bci =>
                bci.SourceEntry.MealPlanId == planId &&
                bci.SourceEntryId != entryId &&
                entryProductIds.Contains(bci.ProductId) &&
                !alreadyLinkedItemIds.Contains(bci.Id))
            .ToListAsync(ct);

        return suggestions.Select(bci =>
        {
            decimal? remaining = null;
            if (bci.TotalQuantity.HasValue)
            {
                var used = bci.Usages.Sum(u => u.QuantityUsed ?? 0);
                remaining = Math.Max(0, bci.TotalQuantity.Value - used);
            }

            return new BatchCookSuggestionDto
            {
                BatchCookItemId = bci.Id,
                ProductId = bci.ProductId,
                ProductName = bci.Product.Name,
                SourceEntryId = bci.SourceEntryId,
                SourceMealName = bci.SourceEntry.Meal?.Name,
                SourceDayOfWeek = bci.SourceEntry.DayOfWeek,
                RemainingQuantity = remaining,
                QuantityUnitName = bci.QuantityUnit?.Name
            };
        }).ToList();
    }

    #endregion

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
                .ThenInclude(e => e.MealType)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItems)
                    .ThenInclude(bci => bci.Product)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItems)
                    .ThenInclude(bci => bci.QuantityUnit)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItems)
                    .ThenInclude(bci => bci.Usages)
                        .ThenInclude(u => u.DependentEntry)
                            .ThenInclude(de => de.Meal)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.BatchCookItemUsages)
                    .ThenInclude(u => u.BatchCookItem)
                        .ThenInclude(bci => bci.Product);
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
                BatchSourceEntryId = e.BatchSourceEntryId,
                BatchCookItems = e.BatchCookItems.Select(MapBatchCookItemToDto).ToList(),
                BatchCookItemUsages = e.BatchCookItemUsages.Select(u => new BatchCookItemUsageDto
                {
                    Id = u.Id,
                    BatchCookItemId = u.BatchCookItemId,
                    DependentEntryId = u.DependentEntryId,
                    DependentEntryMealName = u.DependentEntry?.Meal?.Name,
                    DependentEntryDayOfWeek = u.DependentEntry?.DayOfWeek ?? 0,
                    QuantityUsed = u.QuantityUsed
                }).ToList()
            }).ToList()
        };
    }

    private async Task<MealPlanEntryDto> ReloadEntryAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await _context.MealPlanEntries
            .Include(e => e.Meal)
            .Include(e => e.MealType)
            .Include(e => e.BatchCookItems)
                .ThenInclude(bci => bci.Product)
            .Include(e => e.BatchCookItems)
                .ThenInclude(bci => bci.QuantityUnit)
            .Include(e => e.BatchCookItems)
                .ThenInclude(bci => bci.Usages)
                    .ThenInclude(u => u.DependentEntry)
                        .ThenInclude(de => de.Meal)
            .Include(e => e.BatchCookItemUsages)
                .ThenInclude(u => u.BatchCookItem)
                    .ThenInclude(bci => bci.Product)
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
            BatchSourceEntryId = entry.BatchSourceEntryId,
            BatchCookItems = entry.BatchCookItems.Select(MapBatchCookItemToDto).ToList(),
            BatchCookItemUsages = entry.BatchCookItemUsages.Select(u => new BatchCookItemUsageDto
            {
                Id = u.Id,
                BatchCookItemId = u.BatchCookItemId,
                DependentEntryId = u.DependentEntryId,
                DependentEntryMealName = u.DependentEntry?.Meal?.Name,
                DependentEntryDayOfWeek = u.DependentEntry?.DayOfWeek ?? 0,
                QuantityUsed = u.QuantityUsed
            }).ToList()
        };
    }

    private static void VerifyVersion(MealPlan plan, uint expectedVersion)
    {
        if (plan.Version != expectedVersion)
            throw new MealPlanConcurrencyException(plan.UpdatedByUserId);
    }

    private async Task<BatchCookItemDto> ReloadBatchCookItemAsync(Guid batchCookItemId, CancellationToken ct)
    {
        var item = await _context.BatchCookItems
            .Include(bci => bci.Product)
            .Include(bci => bci.QuantityUnit)
            .Include(bci => bci.Usages)
                .ThenInclude(u => u.DependentEntry)
                    .ThenInclude(e => e.Meal)
            .FirstAsync(bci => bci.Id == batchCookItemId, ct);

        return MapBatchCookItemToDto(item);
    }

    private static BatchCookItemDto MapBatchCookItemToDto(BatchCookItem bci)
    {
        return new BatchCookItemDto
        {
            Id = bci.Id,
            SourceEntryId = bci.SourceEntryId,
            ProductId = bci.ProductId,
            ProductName = bci.Product?.Name ?? string.Empty,
            TotalQuantity = bci.TotalQuantity,
            QuantityUnitId = bci.QuantityUnitId,
            QuantityUnitName = bci.QuantityUnit?.Name,
            Usages = bci.Usages?.Select(u => new BatchCookItemUsageDto
            {
                Id = u.Id,
                BatchCookItemId = u.BatchCookItemId,
                DependentEntryId = u.DependentEntryId,
                DependentEntryMealName = u.DependentEntry?.Meal?.Name,
                DependentEntryDayOfWeek = u.DependentEntry?.DayOfWeek ?? 0,
                QuantityUsed = u.QuantityUsed
            }).ToList() ?? new()
        };
    }

    private static string GetDayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Mon",
        1 => "Tue",
        2 => "Wed",
        3 => "Thu",
        4 => "Fri",
        5 => "Sat",
        6 => "Sun",
        _ => ""
    };

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
