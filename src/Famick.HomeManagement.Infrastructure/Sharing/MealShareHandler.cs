using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Shares a meal: its recipes and products come along as copies of their own, reusing any the
/// household already has from the same origin, and its free-text items as they are. Meal plans don't.
/// </summary>
public class MealShareHandler : IShareableItemHandler
{
    private readonly HomeManagementDbContext _context;
    private readonly RecipeShareHandler _recipes;
    private readonly ProductShareHandler _products;

    public MealShareHandler(HomeManagementDbContext context, RecipeShareHandler recipes, ProductShareHandler products)
    {
        _context = context;
        _recipes = recipes;
        _products = products;
    }

    public ShareableKind Kind => ShareableKind.Meal;

    public SharedItemPreview Preview(SharedItemSnapshot snapshot, string sharedByHouseholdName, bool hasImage) => new()
    {
        Kind = Kind,
        Name = snapshot.Name,
        SharedByHouseholdName = sharedByHouseholdName,
        HasImage = hasImage,
        ItemCount = ((MealSnapshot)snapshot).Items.Count
    };

    public async Task<SharedItemSnapshot?> SnapshotAsync(Guid itemId, ShareSnapshotContext context, CancellationToken ct)
    {
        var meal = await _context.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .ThenInclude(i => i.ProductQuantityUnit)
            .FirstOrDefaultAsync(m => m.Id == itemId, ct);
        if (meal == null)
            return null;

        context.CountEntities();
        context.CountMealItems(meal.Items.Count);

        var items = new List<MealItemSnapshot>();
        foreach (var item in meal.Items.OrderBy(i => i.SortOrder))
        {
            var recipe = item is { ItemType: MealItemType.Recipe, RecipeId: { } recipeId }
                ? await _recipes.SnapshotRecipeAsync(recipeId, context, ct)
                : null;
            var product = item is { ItemType: MealItemType.Product, ProductId: { } productId }
                ? await _products.SnapshotProductAsync(productId, context, ct)
                : null;

            // An item whose recipe or product has since gone has nothing left to share.
            if ((item.ItemType == MealItemType.Recipe && recipe == null) || (item.ItemType == MealItemType.Product && product == null))
                continue;

            items.Add(new MealItemSnapshot
            {
                ItemType = item.ItemType,
                Recipe = recipe,
                Product = product,
                ProductQuantity = item.ProductQuantity,
                ProductQuantityUnit = item.ProductQuantityUnit is { } unit
                    ? new SharedUnit(unit.Name, unit.NamePlural, unit.StandardUnit)
                    : null,
                FreetextDescription = item.FreetextDescription,
                SortOrder = item.SortOrder
            });
        }

        return new MealSnapshot { SourceId = meal.Id, Name = meal.Name, Notes = meal.Notes, Items = items };
    }

    public async Task<SharedCopy> ImportAsync(SharedItemSnapshot snapshot, ShareImportContext context, CancellationToken ct)
    {
        var shared = (MealSnapshot)snapshot;
        var originKey = context.Origin.KeyFor(ShareableKind.Meal, shared.SourceId);
        var existing = await _context.Meals
            .Where(m => m.OriginKey == originKey)
            .Select(m => new { m.Id, m.Name })
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return new SharedCopy(existing.Id, existing.Name, AlreadyHad: true);

        var units = await _context.QuantityUnits.AsNoTracking().Where(u => u.IsActive).ToListAsync(ct);
        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            Name = ShareText.Required(shared.Name, 200, "Shared meal"),
            Notes = ShareText.Clean(shared.Notes, 2000),
            OriginKey = originKey,
            SharedByHouseholdName = context.Origin.SharedByHouseholdName
        };

        foreach (var item in shared.Items)
        {
            var copy = new MealItem
            {
                Id = Guid.NewGuid(),
                MealId = meal.Id,
                ItemType = item.ItemType,
                SortOrder = item.SortOrder
            };

            switch (item.ItemType)
            {
                case MealItemType.Recipe when item.Recipe != null:
                    copy.RecipeId = (await _recipes.ImportRecipeAsync(item.Recipe, context, ct)).Id;
                    break;
                case MealItemType.Product when item.Product != null:
                    copy.ProductId = (await _products.ImportProductAsync(item.Product, context, ct)).Id;
                    copy.ProductQuantity = item.ProductQuantity is > 0 ? item.ProductQuantity : null;
                    copy.ProductQuantityUnitId = item.ProductQuantityUnit is { } unit
                        ? (unit.StandardUnit is { } standard ? units.FirstOrDefault(u => u.StandardUnit == standard) : null)?.Id
                          ?? units.FirstOrDefault(u => string.Equals(u.Name, unit.Name, StringComparison.OrdinalIgnoreCase))?.Id
                        : null;
                    break;
                case MealItemType.Freetext when !string.IsNullOrWhiteSpace(item.FreetextDescription):
                    copy.FreetextDescription = ShareText.Clean(item.FreetextDescription, 500);
                    break;
                default:
                    continue;
            }

            meal.Items.Add(copy);
        }

        _context.Meals.Add(meal);
        await _context.SaveChangesAsync(ct);
        return new SharedCopy(meal.Id, meal.Name, AlreadyHad: false);
    }
}
