using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Famick.HomeManagement.Core.Validators.Common;

namespace Famick.HomeManagement.Infrastructure.Services;

public class RecipeService : IRecipeService
{
    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileUrlService _fileUrlService;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(
        HomeManagementDbContext context,
        IFileStorageService fileStorage,
        IFileUrlService fileUrlService,
        ILogger<RecipeService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _fileUrlService = fileUrlService;
        _logger = logger;
    }

    #region Recipe CRUD

    public async Task<RecipeDto> CreateAsync(CreateRecipeRequest request, CancellationToken ct = default)
    {
        var recipe = RecipeMapper.FromCreateRequest(request);

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created recipe {RecipeId} '{Name}'", recipe.Id, recipe.Name);

        return await ReloadAndMapAsync(recipe.Id, ct);
    }

    public async Task<RecipeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await GetRecipeWithFullIncludes()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return recipe == null ? null : await MapWithIngredientsAsync(recipe, ct);
    }

    public async Task<List<RecipeSummaryDto>> ListAsync(RecipeFilterRequest? filter = null, CancellationToken ct = default)
    {
        var query = _context.Recipes
            .Include(r => r.Steps)
            .Include(r => r.Images)
            .Include(r => r.NestedRecipes)
            .AsQueryable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(term));
            }

            if (filter.IsMeal.HasValue)
            {
                query = query.Where(r => r.IsMeal == filter.IsMeal.Value);
            }

            query = filter.SortBy?.ToLower() switch
            {
                "createdat" => filter.Descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
                "updatedat" => filter.Descending ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt),
                _ => filter.Descending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            };
        }
        else
        {
            query = query.OrderBy(r => r.Name);
        }

        var recipes = await query.ToListAsync(ct);
        return recipes.Select(MapToSummary).ToList();
    }

    public async Task<RecipeDto> UpdateAsync(Guid id, UpdateRecipeRequest request, CancellationToken ct = default)
    {
        var recipe = await _context.Recipes.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {id} not found");

        RecipeMapper.Update(request, recipe);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated recipe {RecipeId}", id);

        return await ReloadAndMapAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {id} not found");

        // Delete image files from storage
        await _fileStorage.DeleteAllRecipeImagesAsync(id, ct);

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted recipe {RecipeId}", id);
    }

    #endregion

    #region Steps

    public async Task<RecipeStepDto> AddStepAsync(Guid recipeId, CreateRecipeStepRequest request, CancellationToken ct = default)
    {
        WebLink.Ensure(request.VideoUrl, nameof(request.VideoUrl));
        var recipe = await _context.Recipes
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {recipeId} not found");

        var step = RecipeMapper.FromCreateStepRequest(request);
        step.RecipeId = recipeId;
        step.StepOrder = recipe.Steps.Count + 1;

        _context.Set<RecipeStep>().Add(step);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added step {StepId} (order {Order}) to recipe {RecipeId}", step.Id, step.StepOrder, recipeId);

        var reloaded = await _context.Set<RecipeStep>()
            .Include(s => s.Ingredients).ThenInclude(i => i.Product)
            .Include(s => s.Ingredients).ThenInclude(i => i.QuantityUnit)
            .FirstAsync(s => s.Id == step.Id, ct);

        return MapToStepDto(reloaded);
    }

    public async Task<RecipeStepDto> UpdateStepAsync(Guid recipeId, Guid stepId, UpdateRecipeStepRequest request, CancellationToken ct = default)
    {
        WebLink.Ensure(request.VideoUrl, nameof(request.VideoUrl));
        var step = await _context.Set<RecipeStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.RecipeId == recipeId, ct)
            ?? throw new KeyNotFoundException($"Step with ID {stepId} not found in recipe {recipeId}");

        RecipeMapper.UpdateStep(request, step);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated step {StepId} in recipe {RecipeId}", stepId, recipeId);

        var reloaded = await _context.Set<RecipeStep>()
            .Include(s => s.Ingredients).ThenInclude(i => i.Product)
            .Include(s => s.Ingredients).ThenInclude(i => i.QuantityUnit)
            .FirstAsync(s => s.Id == stepId, ct);

        return MapToStepDto(reloaded);
    }

    public async Task DeleteStepAsync(Guid recipeId, Guid stepId, CancellationToken ct = default)
    {
        var step = await _context.Set<RecipeStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.RecipeId == recipeId, ct)
            ?? throw new KeyNotFoundException($"Step with ID {stepId} not found in recipe {recipeId}");

        // Delete step image if exists
        if (!string.IsNullOrEmpty(step.ImageFileName))
        {
            await _fileStorage.DeleteRecipeStepImageAsync(recipeId, stepId, step.ImageFileName, ct);
        }

        var deletedOrder = step.StepOrder;
        _context.Set<RecipeStep>().Remove(step);
        await _context.SaveChangesAsync(ct);

        // Reorder remaining steps
        var remainingSteps = await _context.Set<RecipeStep>()
            .Where(s => s.RecipeId == recipeId && s.StepOrder > deletedOrder)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(ct);

        foreach (var s in remainingSteps)
        {
            s.StepOrder--;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted step {StepId} from recipe {RecipeId}, reordered remaining steps", stepId, recipeId);
    }

    public async Task ReorderStepsAsync(Guid recipeId, ReorderStepsRequest request, CancellationToken ct = default)
    {
        var steps = await _context.Set<RecipeStep>()
            .Where(s => s.RecipeId == recipeId)
            .ToListAsync(ct);

        var stepDict = steps.ToDictionary(s => s.Id);

        for (var i = 0; i < request.StepIds.Count; i++)
        {
            if (stepDict.TryGetValue(request.StepIds[i], out var step))
            {
                step.StepOrder = i + 1;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Reordered {Count} steps in recipe {RecipeId}", request.StepIds.Count, recipeId);
    }

    #endregion

    #region Ingredients

    public async Task<RecipeIngredientDto> AddIngredientAsync(Guid recipeId, Guid stepId, CreateRecipeIngredientRequest request, CancellationToken ct = default)
    {
        var step = await _context.Set<RecipeStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.RecipeId == recipeId, ct)
            ?? throw new KeyNotFoundException($"Step with ID {stepId} not found in recipe {recipeId}");

        var ingredient = RecipeMapper.FromCreateIngredientRequest(request);
        ingredient.RecipeStepId = stepId;

        _context.Set<RecipePosition>().Add(ingredient);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added ingredient {IngredientId} to step {StepId} in recipe {RecipeId}", ingredient.Id, stepId, recipeId);

        var reloaded = await _context.Set<RecipePosition>()
            .Include(i => i.Product)
            .Include(i => i.QuantityUnit)
            .FirstAsync(i => i.Id == ingredient.Id, ct);

        return MapToIngredientDto(reloaded);
    }

    public async Task<RecipeIngredientDto> UpdateIngredientAsync(Guid ingredientId, UpdateRecipeIngredientRequest request, CancellationToken ct = default)
    {
        var ingredient = await _context.Set<RecipePosition>()
            .FirstOrDefaultAsync(i => i.Id == ingredientId, ct)
            ?? throw new KeyNotFoundException($"Ingredient with ID {ingredientId} not found");

        RecipeMapper.UpdateIngredient(request, ingredient);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated ingredient {IngredientId}", ingredientId);

        var reloaded = await _context.Set<RecipePosition>()
            .Include(i => i.Product)
            .Include(i => i.QuantityUnit)
            .FirstAsync(i => i.Id == ingredientId, ct);

        return MapToIngredientDto(reloaded);
    }

    public async Task DeleteIngredientAsync(Guid ingredientId, CancellationToken ct = default)
    {
        var ingredient = await _context.Set<RecipePosition>()
            .FirstOrDefaultAsync(i => i.Id == ingredientId, ct)
            ?? throw new KeyNotFoundException($"Ingredient with ID {ingredientId} not found");

        _context.Set<RecipePosition>().Remove(ingredient);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted ingredient {IngredientId}", ingredientId);
    }

    #endregion

    #region Images

    public async Task<RecipeImageDto> AddImageAsync(Guid recipeId, Stream stream, string fileName, string contentType, long fileSize, CancellationToken ct = default)
    {
        _ = await _context.Recipes.FindAsync([recipeId], ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {recipeId} not found");

        var storedFileName = await _fileStorage.SaveRecipeImageAsync(recipeId, stream, fileName, ct);

        var existingCount = await _context.Set<RecipeImage>()
            .CountAsync(i => i.RecipeId == recipeId, ct);

        var image = new RecipeImage
        {
            RecipeId = recipeId,
            FileName = storedFileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileSize,
            SortOrder = existingCount,
            IsPrimary = existingCount == 0
        };

        _context.Set<RecipeImage>().Add(image);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added image {ImageId} to recipe {RecipeId}", image.Id, recipeId);

        return MapToImageDto(image);
    }

    public async Task DeleteImageAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _context.Set<RecipeImage>()
            .FirstOrDefaultAsync(i => i.Id == imageId, ct)
            ?? throw new KeyNotFoundException($"Image with ID {imageId} not found");

        await _fileStorage.DeleteRecipeImageAsync(image.RecipeId, image.FileName, ct);

        var wasPrimary = image.IsPrimary;
        var recipeId = image.RecipeId;

        _context.Set<RecipeImage>().Remove(image);
        await _context.SaveChangesAsync(ct);

        // If deleted image was primary, promote the first remaining image
        if (wasPrimary)
        {
            var firstImage = await _context.Set<RecipeImage>()
                .Where(i => i.RecipeId == recipeId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync(ct);

            if (firstImage != null)
            {
                firstImage.IsPrimary = true;
                await _context.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Deleted image {ImageId} from recipe {RecipeId}", imageId, recipeId);
    }

    public async Task SetPrimaryImageAsync(Guid recipeId, Guid imageId, CancellationToken ct = default)
    {
        var images = await _context.Set<RecipeImage>()
            .Where(i => i.RecipeId == recipeId)
            .ToListAsync(ct);

        foreach (var img in images)
        {
            img.IsPrimary = img.Id == imageId;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Set image {ImageId} as primary for recipe {RecipeId}", imageId, recipeId);
    }

    public async Task<RecipeImageDto?> GetImageByIdAsync(Guid recipeId, Guid imageId, CancellationToken ct = default)
    {
        var image = await _context.Set<RecipeImage>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.RecipeId == recipeId, ct);

        return image == null ? null : MapToImageDto(image);
    }

    #endregion

    #region Step Images

    public async Task<RecipeStepDto> UploadStepImageAsync(Guid recipeId, Guid stepId, Stream stream, string fileName, string contentType, long fileSize, CancellationToken ct = default)
    {
        var step = await _context.Set<RecipeStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.RecipeId == recipeId, ct)
            ?? throw new KeyNotFoundException($"Step with ID {stepId} not found in recipe {recipeId}");

        // Delete existing step image if present
        if (!string.IsNullOrEmpty(step.ImageFileName))
        {
            await _fileStorage.DeleteRecipeStepImageAsync(recipeId, stepId, step.ImageFileName, ct);
        }

        var storedFileName = await _fileStorage.SaveRecipeStepImageAsync(recipeId, stepId, stream, fileName, ct);

        step.ImageFileName = storedFileName;
        step.ImageOriginalFileName = fileName;
        step.ImageContentType = contentType;
        step.ImageFileSize = fileSize;
        step.ImageExternalUrl = null; // Clear external URL when uploading local file

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Uploaded step image for recipe {RecipeId} step {StepId}", recipeId, stepId);

        var reloaded = await _context.Set<RecipeStep>()
            .Include(s => s.Ingredients).ThenInclude(i => i.Product)
            .Include(s => s.Ingredients).ThenInclude(i => i.QuantityUnit)
            .FirstAsync(s => s.Id == stepId, ct);

        return MapToStepDto(reloaded);
    }

    public async Task DeleteStepImageAsync(Guid recipeId, Guid stepId, CancellationToken ct = default)
    {
        var step = await _context.Set<RecipeStep>()
            .FirstOrDefaultAsync(s => s.Id == stepId && s.RecipeId == recipeId, ct)
            ?? throw new KeyNotFoundException($"Step with ID {stepId} not found in recipe {recipeId}");

        if (!string.IsNullOrEmpty(step.ImageFileName))
        {
            await _fileStorage.DeleteRecipeStepImageAsync(recipeId, stepId, step.ImageFileName, ct);
        }

        step.ImageFileName = null;
        step.ImageOriginalFileName = null;
        step.ImageContentType = null;
        step.ImageFileSize = null;
        step.ImageExternalUrl = null;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted step image for recipe {RecipeId} step {StepId}", recipeId, stepId);
    }

    #endregion

    #region Nesting

    public async Task AddNestedRecipeAsync(Guid parentRecipeId, Guid childRecipeId, CancellationToken ct = default)
    {
        if (parentRecipeId == childRecipeId)
            throw new InvalidOperationException("A recipe cannot include itself");

        _ = await _context.Recipes.FindAsync([parentRecipeId], ct)
            ?? throw new KeyNotFoundException($"Parent recipe with ID {parentRecipeId} not found");

        _ = await _context.Recipes.FindAsync([childRecipeId], ct)
            ?? throw new KeyNotFoundException($"Child recipe with ID {childRecipeId} not found");

        // Check for circular dependency
        var hierarchy = await GetHierarchyInternalAsync(childRecipeId, ct);
        if (hierarchy.Contains(parentRecipeId))
            throw new InvalidOperationException("Adding this nested recipe would create a circular dependency");

        // Check for duplicate
        var existing = await _context.Set<RecipeNesting>()
            .AnyAsync(n => n.RecipeId == parentRecipeId && n.IncludesRecipeId == childRecipeId, ct);

        if (existing)
            throw new InvalidOperationException("This recipe is already nested");

        var nesting = new RecipeNesting
        {
            RecipeId = parentRecipeId,
            IncludesRecipeId = childRecipeId
        };

        _context.Set<RecipeNesting>().Add(nesting);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added nested recipe {ChildId} to parent {ParentId}", childRecipeId, parentRecipeId);
    }

    public async Task RemoveNestedRecipeAsync(Guid parentRecipeId, Guid childRecipeId, CancellationToken ct = default)
    {
        var nesting = await _context.Set<RecipeNesting>()
            .FirstOrDefaultAsync(n => n.RecipeId == parentRecipeId && n.IncludesRecipeId == childRecipeId, ct)
            ?? throw new KeyNotFoundException($"Nested recipe relationship not found");

        _context.Set<RecipeNesting>().Remove(nesting);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Removed nested recipe {ChildId} from parent {ParentId}", childRecipeId, parentRecipeId);
    }

    #endregion

    #region Sharing

    public async Task<RecipeShareDto> GenerateShareTokenAsync(Guid recipeId, CancellationToken ct = default)
    {
        _ = await _context.Recipes.FindAsync([recipeId], ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {recipeId} not found");

        // Revoke any existing active tokens
        var activeTokens = await _context.Set<RecipeShareToken>()
            .Where(t => t.RecipeId == recipeId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
        }

        var shareToken = new RecipeShareToken
        {
            RecipeId = recipeId,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            IsRevoked = false
        };

        _context.Set<RecipeShareToken>().Add(shareToken);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Generated share token for recipe {RecipeId}, expires {ExpiresAt}", recipeId, shareToken.ExpiresAt);

        return MapToShareDto(shareToken);
    }

    public async Task RevokeShareTokenAsync(Guid recipeId, CancellationToken ct = default)
    {
        var activeTokens = await _context.Set<RecipeShareToken>()
            .Where(t => t.RecipeId == recipeId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked {Count} share token(s) for recipe {RecipeId}", activeTokens.Count, recipeId);
    }

    public async Task<RecipeDto?> GetByShareTokenAsync(string token, CancellationToken ct = default)
    {
        var shareToken = await _context.Set<RecipeShareToken>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (shareToken == null || shareToken.IsRevoked || shareToken.ExpiresAt <= DateTime.UtcNow)
            return null;

        var recipe = await _context.Recipes
            .IgnoreQueryFilters()
            .Where(r => r.Id == shareToken.RecipeId && r.TenantId == shareToken.TenantId)
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Ingredients.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.Product)
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Ingredients.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.QuantityUnit)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.CreatedByContact)
            .FirstOrDefaultAsync(ct);

        if (recipe == null)
            return null;

        // A share link has no tenant context to resolve nested recipes, so only this recipe's own steps count.
        var dto = MapToDto(recipe);
        dto.ConsolidatedIngredients = ConsolidateIngredients(OrderedIngredients(recipe), 1m);
        return dto;
    }

    #endregion

    #region Business Logic

    public async Task<RecipeFulfillmentDto> CheckFulfillmentAsync(Guid recipeId, int? servings = null, CancellationToken ct = default)
    {
        var recipe = await _context.Recipes.FindAsync([recipeId], ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {recipeId} not found");

        var ingredients = await GetAggregatedIngredientsAsync(recipeId, servings, ct);
        var checks = await CompareWithStockAsync(ingredients, ct);

        var fulfillments = checks.Select(c => new IngredientFulfillmentDto
        {
            ProductId = c.Line.ProductId,
            ProductName = c.Line.ProductName,
            RequiredAmount = ProductUnits.Tidy(c.Line.TotalAmount),
            AvailableAmount = c.Available,
            IsSufficient = c.IsSufficient,
            QuantityUnitId = c.Line.QuantityUnitId,
            QuantityUnitName = c.Line.QuantityUnitName,
            AvailableQuantityUnitName = c.AvailableUnitName,
            CanCompare = c.CanCompare
        }).ToList();

        return new RecipeFulfillmentDto
        {
            RecipeId = recipeId,
            RecipeName = recipe.Name,
            CanBeMade = fulfillments.All(f => f.IsSufficient),
            HasUncomparableIngredients = fulfillments.Any(f => !f.CanCompare),
            Ingredients = fulfillments
        };
    }

    public async Task<List<IngredientRequirementDto>> GetAggregatedIngredientsAsync(Guid recipeId, int? servings = null, CancellationToken ct = default)
    {
        var recipe = await GetRecipeWithIngredients()
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct)
            ?? throw new KeyNotFoundException($"Recipe with ID {recipeId} not found");

        var scaleFactor = servings.HasValue ? (decimal)servings.Value / recipe.Servings : 1m;

        // Stock checks and the shopping list skip ingredients marked as not stock-checked (salt, water).
        var ingredients = (await CollectIngredientsAsync(recipe, ct))
            .Where(i => !i.NotCheckStockFulfillment);

        return ConsolidateIngredients(ingredients, scaleFactor);
    }

    public async Task AddToShoppingListAsync(Guid recipeId, AddToShoppingListRequest request, CancellationToken ct = default)
    {
        var shoppingList = await _context.Set<ShoppingList>()
            .Include(sl => sl.Items)
            .FirstOrDefaultAsync(sl => sl.Id == request.ShoppingListId, ct)
            ?? throw new KeyNotFoundException($"Shopping list with ID {request.ShoppingListId} not found");

        var ingredients = await GetAggregatedIngredientsAsync(recipeId, request.Servings, ct);
        var checks = await CompareWithStockAsync(ingredients, ct);

        // What to buy, in each product's purchase unit: the lines that convert are short by an amount in the
        // stock unit, rounded up to whole purchase units (2 cups of flour is 1 bag). A line that doesn't
        // convert is left out: its shortfall is in a unit the list can't buy in (100 g of flour isn't 100
        // bags), and the dialog shows it as one it can't compare. When only some products are asked for, the
        // whole recipe is still compared, so those products share the stock as they do in it.
        var shortfalls = new Dictionary<Guid, decimal>();
        foreach (var product in checks.Where(c => c.CanCompare && c.Product != null).GroupBy(c => c.Line.ProductId))
        {
            if (request.ProductIds != null && !request.ProductIds.Contains(product.Key))
                continue;

            var toBuy = ProductUnits.ToPurchaseToBuy(product.First().Product!, product.Sum(c => c.StockShortfall));

            if (toBuy > 0)
                shortfalls[product.Key] = toBuy;
        }

        foreach (var (productId, shortfall) in shortfalls)
        {
            // What is on the list and not yet bought already covers part of the shortfall, so adding the same
            // recipe twice adds nothing the second time. That includes another recipe's items: a second recipe
            // needing the same product isn't added on top, just as each recipe is compared with all the stock.
            // A child product checked off against an item counts as bought, as its own purchases do.
            var items = shoppingList.Items.Where(i => i.ProductId == productId).ToList();
            var onList = items.Sum(ShoppingListItemQuantities.Remaining);

            var toAdd = shortfall - onList;
            if (toAdd <= 0)
                continue;

            // Onto an item still to buy; a purchased one is done, and an amount added to it would go unseen.
            var openItem = items.FirstOrDefault(i => !i.IsPurchased);
            if (openItem != null)
            {
                // From what was bought, if more than the amount: the two kinds of purchase are each compared with
                // the amount on their own, so an open item can be over-bought, and the extra mustn't eat the top-up.
                openItem.Amount = Math.Max(openItem.Amount, ShoppingListItemQuantities.Bought(openItem)) + toAdd;
            }
            else
            {
                var newItem = new ShoppingListItem
                {
                    ShoppingListId = request.ShoppingListId,
                    ProductId = productId,
                    Amount = toAdd,
                    TenantId = shoppingList.TenantId
                };
                _context.Set<ShoppingListItem>().Add(newItem);
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added recipe {RecipeId} ingredients to shopping list {ShoppingListId}", recipeId, request.ShoppingListId);
    }

    public async Task<List<Guid>> GetHierarchyAsync(Guid recipeId, CancellationToken ct = default)
    {
        return await GetHierarchyInternalAsync(recipeId, ct);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// How one recipe line compares with its product's stock. <see cref="StockShortfall"/> is in the
    /// product's stock unit, and is 0 for a line whose unit doesn't convert.
    /// </summary>
    private sealed record LineStock(
        IngredientRequirementDto Line,
        Product? Product,
        bool CanCompare,
        decimal Available,
        string? AvailableUnitName,
        bool IsSufficient,
        decimal StockShortfall);

    /// <summary>
    /// Compares each line with its product's stock: the product's own and its children's, in its stock
    /// unit. A line in another unit is converted to the stock unit first, through the purchase-to-stock
    /// factor or the standard measures. A product's lines then share its stock in recipe order, so
    /// "1 cup + 2 tbsp" of flour against 1 cup is short by the tablespoons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A child's stock is counted once, even when a recipe names both a product and one of its children.
    /// Lines for a child go first, since only that child's stock will do for them, while a line for the
    /// parent can use any child's. A parent line uses its own stock before its children's.
    /// </para>
    /// <para>
    /// A line that can't be converted is compared with the whole stock as it is, as every line used to be.
    /// It takes nothing from the stock the other lines share, since its amount means nothing in that unit.
    /// </para>
    /// </remarks>
    private async Task<List<LineStock>> CompareWithStockAsync(List<IngredientRequirementDto> lines, CancellationToken ct)
    {
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        var children = await _context.Products
            .Where(p => p.ParentProductId != null && productIds.Contains(p.ParentProductId.Value))
            .ToListAsync(ct);

        var stockedIds = productIds.Concat(children.Select(c => c.Id)).ToList();
        var stockByProduct = await _context.Set<StockEntry>()
            .Where(s => stockedIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Amount = g.Sum(s => s.Amount) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Amount, ct);

        var unitIds = lines.Where(l => l.QuantityUnitId.HasValue).Select(l => l.QuantityUnitId!.Value)
            .Concat(products.Values.SelectMany(p => new[] { p.QuantityUnitIdStock, p.QuantityUnitIdPurchase }))
            .Concat(children.Select(c => c.QuantityUnitIdStock))
            .Distinct()
            .ToList();
        var units = await _context.QuantityUnits
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // What is left of each product's own stock, in its own stock unit, and what a line for a product can
        // draw on: its own stock, and its children's in its stock unit. A child whose stock unit doesn't
        // convert to its parent's can't be added in, and is left out rather than counted as if the units matched.
        var left = stockByProduct.ToDictionary(s => s.Key, s => Math.Max(s.Value, 0));
        var childrenOf = children.ToLookup(c => c.ParentProductId!.Value);

        decimal Available(Product product) =>
            left.GetValueOrDefault(product.Id)
            + childrenOf[product.Id].Sum(c => ProductUnits.ChildToParentStock(product, c, left.GetValueOrDefault(c.Id), units) ?? 0);

        void Take(Product product, decimal amount)
        {
            var own = Math.Min(left.GetValueOrDefault(product.Id), amount);
            left[product.Id] = left.GetValueOrDefault(product.Id) - own;
            amount -= own;
            foreach (var child in childrenOf[product.Id])
            {
                if (amount <= 0)
                    break;
                var childLeft = left.GetValueOrDefault(child.Id);
                var inParent = ProductUnits.ChildToParentStock(product, child, childLeft, units) ?? 0;
                if (inParent <= 0)
                    continue;
                var taken = Math.Min(inParent, amount);
                left[child.Id] = childLeft - childLeft * taken / inParent;
                amount -= taken;
            }
        }

        var whole = products.ToDictionary(p => p.Key, p => Available(p.Value));
        var result = new LineStock[lines.Count];
        var order = Enumerable.Range(0, lines.Count)
            .OrderBy(i => products.GetValueOrDefault(lines[i].ProductId)?.ParentProductId == null ? 1 : 0);
        foreach (var i in order)
        {
            var line = lines[i];
            var product = products.GetValueOrDefault(line.ProductId);
            var required = product == null ? null : ProductUnits.ToStock(product, line.QuantityUnitId, line.TotalAmount, units);

            if (product != null && required is { } requiredStock)
            {
                var available = Available(product);
                Take(product, requiredStock);

                result[i] = new LineStock(
                    line, product, CanCompare: true,
                    Available: ProductUnits.Tidy(ProductUnits.FromStock(product, line.QuantityUnitId, available, units) ?? available),
                    AvailableUnitName: line.QuantityUnitName,
                    IsSufficient: available >= requiredStock,
                    StockShortfall: Math.Max(requiredStock - available, 0));
            }
            else
            {
                var all = whole.GetValueOrDefault(line.ProductId);
                var stockUnitName = product != null && units.TryGetValue(product.QuantityUnitIdStock, out var stockUnit)
                    ? stockUnit.Name
                    : line.QuantityUnitName;

                result[i] = new LineStock(
                    line, product, CanCompare: false,
                    Available: ProductUnits.Tidy(all),
                    AvailableUnitName: stockUnitName,
                    IsSufficient: all >= line.TotalAmount,
                    StockShortfall: 0);
            }
        }

        return result.ToList();
    }

    private IQueryable<Recipe> GetRecipeWithFullIncludes()
    {
        return _context.Recipes
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Ingredients.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.Product)
            .Include(r => r.Steps.OrderBy(s => s.StepOrder))
                .ThenInclude(s => s.Ingredients.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.QuantityUnit)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.NestedRecipes)
                .ThenInclude(n => n.IncludedRecipe)
            .Include(r => r.CreatedByContact)
            .Include(r => r.ShareTokens);
    }

    private async Task<RecipeDto> ReloadAndMapAsync(Guid recipeId, CancellationToken ct)
    {
        var recipe = await GetRecipeWithFullIncludes()
            .FirstAsync(r => r.Id == recipeId, ct);

        return await MapWithIngredientsAsync(recipe, ct);
    }

    private async Task<RecipeDto> MapWithIngredientsAsync(Recipe recipe, CancellationToken ct)
    {
        var dto = MapToDto(recipe);
        dto.ConsolidatedIngredients = ConsolidateIngredients(await CollectIngredientsAsync(recipe, ct), 1m);
        return dto;
    }

    private IQueryable<Recipe> GetRecipeWithIngredients()
    {
        return _context.Recipes
            .Include(r => r.Steps)
                .ThenInclude(s => s.Ingredients)
                    .ThenInclude(i => i.Product)
            .Include(r => r.Steps)
                .ThenInclude(s => s.Ingredients)
                    .ThenInclude(i => i.QuantityUnit);
    }

    /// <summary>
    /// The ingredients of a loaded recipe's steps, then those of every recipe nested under it, in step order.
    /// </summary>
    private async Task<List<RecipePosition>> CollectIngredientsAsync(Recipe recipe, CancellationToken ct)
    {
        var ingredients = OrderedIngredients(recipe).ToList();

        var hierarchy = await GetHierarchyInternalAsync(recipe.Id, ct);
        foreach (var nestedId in hierarchy.Where(id => id != recipe.Id))
        {
            var nested = await GetRecipeWithIngredients()
                .FirstOrDefaultAsync(r => r.Id == nestedId, ct);

            if (nested != null)
            {
                ingredients.AddRange(OrderedIngredients(nested));
            }
        }

        return ingredients;
    }

    private static IEnumerable<RecipePosition> OrderedIngredients(Recipe recipe) =>
        recipe.Steps
            .OrderBy(s => s.StepOrder)
            .SelectMany(s => s.Ingredients.OrderBy(i => i.SortOrder));

    /// <summary>
    /// Sums the ingredients by product and unit, keeping the order each line first appears in. The same
    /// product in two units stays as two lines, as the recipe wrote them; the stock check converts them.
    /// </summary>
    private static List<IngredientRequirementDto> ConsolidateIngredients(IEnumerable<RecipePosition> ingredients, decimal scaleFactor)
    {
        return ingredients
            .GroupBy(i => (i.ProductId, i.QuantityUnitId))
            .Select(g =>
            {
                var notes = g
                    .Select(i => i.Note?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new IngredientRequirementDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.First().Product?.Name ?? string.Empty,
                    TotalAmount = g.Sum(i => i.Amount) * scaleFactor,
                    QuantityUnitId = g.Key.QuantityUnitId,
                    QuantityUnitName = g.First().QuantityUnit?.Name,
                    IngredientGroup = g.First().IngredientGroup,
                    Note = notes.Count > 0 ? string.Join("; ", notes) : null
                };
            })
            .ToList();
    }

    private RecipeDto MapToDto(Recipe recipe)
    {
        return new RecipeDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Source = recipe.Source,
            Servings = recipe.Servings,
            Notes = recipe.Notes,
            Attribution = recipe.Attribution,
            IsMeal = recipe.IsMeal,
            CreatedByContactId = recipe.CreatedByContactId,
            CreatedByContactName = recipe.CreatedByContact?.FirstName != null
                ? $"{recipe.CreatedByContact.FirstName} {recipe.CreatedByContact.LastName}".Trim()
                : null,
            Steps = recipe.Steps
                .OrderBy(s => s.StepOrder)
                .Select(MapToStepDto)
                .ToList(),
            Images = recipe.Images
                .OrderBy(i => i.SortOrder)
                .Select(MapToImageDto)
                .ToList(),
            NestedRecipes = recipe.NestedRecipes
                .Select(n => new NestedRecipeDto
                {
                    RecipeId = n.IncludesRecipeId,
                    RecipeName = n.IncludedRecipe?.Name ?? string.Empty
                })
                .ToList(),
            ShareTokens = recipe.ShareTokens
                .Where(t => !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
                .Select(MapToShareDto)
                .ToList(),
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt ?? recipe.CreatedAt
        };
    }

    private RecipeSummaryDto MapToSummary(Recipe recipe)
    {
        var primaryImage = recipe.Images?.FirstOrDefault(i => i.IsPrimary)
            ?? recipe.Images?.OrderBy(i => i.SortOrder).FirstOrDefault();

        string? primaryImageUrl = null;
        if (primaryImage != null)
        {
            primaryImageUrl = _fileUrlService.GetRecipeImageUrl(
                recipe.Id, primaryImage.Id, primaryImage.TenantId,
                primaryImage.ExternalUrl, primaryImage.FileName);
        }

        return new RecipeSummaryDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Source = recipe.Source,
            Servings = recipe.Servings,
            IsMeal = recipe.IsMeal,
            PrimaryImageUrl = primaryImageUrl,
            StepCount = recipe.Steps?.Count ?? 0,
            NestedRecipeCount = recipe.NestedRecipes?.Count ?? 0,
            UpdatedAt = recipe.UpdatedAt ?? recipe.CreatedAt
        };
    }

    private RecipeStepDto MapToStepDto(RecipeStep step)
    {
        var imageUrl = _fileUrlService.GetRecipeStepImageUrl(
            step.RecipeId, step.Id, step.TenantId,
            step.ImageExternalUrl, step.ImageFileName);

        return new RecipeStepDto
        {
            Id = step.Id,
            RecipeId = step.RecipeId,
            StepOrder = step.StepOrder,
            Title = step.Title,
            Description = step.Description,
            Instructions = step.Instructions,
            ImageFileName = step.ImageFileName,
            ImageOriginalFileName = step.ImageOriginalFileName,
            ImageContentType = step.ImageContentType,
            ImageFileSize = step.ImageFileSize,
            ImageUrl = imageUrl,
            ImageExternalUrl = step.ImageExternalUrl,
            VideoUrl = step.VideoUrl,
            Ingredients = step.Ingredients
                .OrderBy(i => i.SortOrder)
                .Select(MapToIngredientDto)
                .ToList()
        };
    }

    private static RecipeIngredientDto MapToIngredientDto(RecipePosition ingredient)
    {
        return new RecipeIngredientDto
        {
            Id = ingredient.Id,
            RecipeStepId = ingredient.RecipeStepId,
            ProductId = ingredient.ProductId,
            ProductName = ingredient.Product?.Name ?? string.Empty,
            Amount = ingredient.Amount,
            AmountInGrams = ingredient.AmountInGrams,
            QuantityUnitId = ingredient.QuantityUnitId,
            QuantityUnitName = ingredient.QuantityUnit?.Name,
            Note = ingredient.Note,
            IngredientGroup = ingredient.IngredientGroup,
            OnlyCheckSingleUnitInStock = ingredient.OnlyCheckSingleUnitInStock,
            NotCheckStockFulfillment = ingredient.NotCheckStockFulfillment,
            SortOrder = ingredient.SortOrder
        };
    }

    private RecipeImageDto MapToImageDto(RecipeImage image)
    {
        return new RecipeImageDto
        {
            Id = image.Id,
            RecipeId = image.RecipeId,
            TenantId = image.TenantId,
            FileName = image.FileName,
            OriginalFileName = image.OriginalFileName,
            ContentType = image.ContentType,
            FileSize = image.FileSize,
            SortOrder = image.SortOrder,
            IsPrimary = image.IsPrimary,
            Url = _fileUrlService.GetRecipeImageUrl(image.RecipeId, image.Id, image.TenantId, null, image.FileName) ?? string.Empty,
            ExternalUrl = image.ExternalUrl,
            ExternalThumbnailUrl = image.ExternalThumbnailUrl,
            ExternalSource = image.ExternalSource,
            CreatedAt = image.CreatedAt
        };
    }

    private static RecipeShareDto MapToShareDto(RecipeShareToken token)
    {
        return new RecipeShareDto
        {
            Id = token.Id,
            RecipeId = token.RecipeId,
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            IsRevoked = token.IsRevoked,
            ShareUrl = $"/api/v1/recipes/shared/{token.Token}",
            CreatedAt = token.CreatedAt
        };
    }

    private async Task<List<Guid>> GetHierarchyInternalAsync(Guid recipeId, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(recipeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            var children = await _context.Set<RecipeNesting>()
                .Where(n => n.RecipeId == current)
                .Select(n => n.IncludesRecipeId)
                .ToListAsync(ct);

            foreach (var child in children)
            {
                queue.Enqueue(child);
            }
        }

        return visited.ToList();
    }

    #endregion
}
