using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Shares a recipe with its steps, ingredient lines, pictures and nested recipes. Lines are matched to
/// the receiving household's own products, or the catalog's, as an imported recipe's are; a line with
/// no match is kept as its text.
/// </summary>
public class RecipeShareHandler : IShareableItemHandler
{
    public const string ImageExternalSource = "share";

    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IngredientMatcher _matcher;

    public RecipeShareHandler(HomeManagementDbContext context, IFileStorageService storage, IngredientMatcher matcher)
    {
        _context = context;
        _storage = storage;
        _matcher = matcher;
    }

    public ShareableKind Kind => ShareableKind.Recipe;

    public async Task<SharedItemSnapshot?> SnapshotAsync(Guid itemId, ShareSnapshotContext context, CancellationToken ct) =>
        await SnapshotRecipeAsync(itemId, context, ct);

    public async Task<SharedCopy> ImportAsync(SharedItemSnapshot snapshot, ShareImportContext context, CancellationToken ct) =>
        await ImportRecipeAsync((RecipeSnapshot)snapshot, context, ct);

    public SharedItemPreview Preview(SharedItemSnapshot snapshot, string sharedByHouseholdName, bool hasImage)
    {
        var recipe = (RecipeSnapshot)snapshot;
        return new SharedItemPreview
        {
            Kind = Kind,
            Name = recipe.Name,
            SharedByHouseholdName = sharedByHouseholdName,
            HasImage = hasImage,
            IngredientCount = AllLines(recipe).Count(),
            StepCount = recipe.Steps.Count
        };
    }

    private static IEnumerable<IngredientSnapshot> AllLines(RecipeSnapshot recipe) =>
        recipe.Steps.SelectMany(s => s.Ingredients).Concat(recipe.Nested.SelectMany(AllLines));

    /// <summary>The recipe and those it nests, read in the current household.</summary>
    internal Task<RecipeSnapshot?> SnapshotRecipeAsync(Guid recipeId, ShareSnapshotContext context, CancellationToken ct) =>
        SnapshotRecipeAsync(recipeId, context, depth: 0, path: new HashSet<Guid>(), ct);

    private async Task<RecipeSnapshot?> SnapshotRecipeAsync(
        Guid recipeId, ShareSnapshotContext context, int depth, HashSet<Guid> path, CancellationToken ct)
    {
        // A recipe that nests one of the recipes it is nested in stops there.
        if (!path.Add(recipeId))
            return null;

        var recipe = await _context.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Images)
            .Include(r => r.NestedRecipes)
            .Include(r => r.Steps).ThenInclude(s => s.Ingredients).ThenInclude(i => i.Product!).ThenInclude(p => p.MasterProduct)
            .Include(r => r.Steps).ThenInclude(s => s.Ingredients).ThenInclude(i => i.QuantityUnit)
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct);
        if (recipe == null)
            return null;

        context.CountEntities();
        context.CountSteps(recipe.Steps.Count);
        context.CountIngredientLines(recipe.Steps.Sum(s => s.Ingredients.Count));

        var images = new List<SharedImageSnapshot>();
        foreach (var image in recipe.Images.OrderBy(i => i.SortOrder))
        {
            var key = string.IsNullOrEmpty(image.FileName)
                ? null
                : await context.AddImageAsync(c => _storage.GetRecipeImageStreamAsync(recipe.Id, image.FileName, c), ct);
            if (key != null)
                images.Add(new SharedImageSnapshot(key, image.ExternalUrl, image.ExternalThumbnailUrl, image.IsPrimary));
        }

        var steps = new List<RecipeStepSnapshot>();
        foreach (var step in recipe.Steps.OrderBy(s => s.StepOrder))
        {
            var imageKey = string.IsNullOrEmpty(step.ImageFileName)
                ? null
                : await context.AddImageAsync(c => _storage.GetRecipeStepImageStreamAsync(recipe.Id, step.Id, step.ImageFileName, c), ct);

            steps.Add(new RecipeStepSnapshot
            {
                StepOrder = step.StepOrder,
                Title = step.Title,
                Description = step.Description,
                Instructions = step.Instructions,
                VideoUrl = step.VideoUrl,
                ImageKey = imageKey,
                ImageExternalUrl = step.ImageExternalUrl,
                Ingredients = step.Ingredients.OrderBy(i => i.SortOrder).Select(i => new IngredientSnapshot
                {
                    OriginalText = i.OriginalText,
                    ProductName = i.Product?.Name,
                    // Only a published catalog product: a contribution still in review stays the sharer's.
                    MasterProductId = i.Product?.MasterProduct is { ReviewStatus: MasterProductReviewStatus.Approved } master
                        ? master.Id
                        : null,
                    Amount = i.Amount,
                    AmountInGrams = i.AmountInGrams,
                    Unit = i.QuantityUnit is { } unit ? new SharedUnit(unit.Name, unit.NamePlural, unit.StandardUnit) : null,
                    Note = i.Note,
                    IngredientGroup = i.IngredientGroup,
                    OnlyCheckSingleUnitInStock = i.OnlyCheckSingleUnitInStock,
                    NotCheckStockFulfillment = i.NotCheckStockFulfillment,
                    SortOrder = i.SortOrder
                }).ToList()
            });
        }

        var nested = new List<RecipeSnapshot>();
        foreach (var nesting in recipe.NestedRecipes)
        {
            context.CountNestedRecipe(depth + 1);
            if (await SnapshotRecipeAsync(nesting.IncludesRecipeId, context, depth + 1, path, ct) is { } included)
                nested.Add(included);
        }

        path.Remove(recipeId);

        return new RecipeSnapshot
        {
            SourceId = recipe.Id,
            Name = recipe.Name,
            Source = recipe.Source,
            Servings = recipe.Servings,
            Notes = recipe.Notes,
            Attribution = recipe.Attribution,
            IsMeal = recipe.IsMeal,
            Images = images,
            Steps = steps,
            Nested = nested
        };
    }

    /// <summary>
    /// The household's copy of the recipe: the one it already has from this origin, else a new one, saved
    /// before it returns so a recipe shared twice within one copy is found the second time.
    /// </summary>
    internal async Task<SharedCopy> ImportRecipeAsync(RecipeSnapshot shared, ShareImportContext context, CancellationToken ct)
    {
        var originKey = context.Origin.KeyFor(ShareableKind.Recipe, shared.SourceId);
        var existing = await _context.Recipes
            .Where(r => r.OriginKey == originKey)
            .Select(r => new { r.Id, r.Name })
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return new SharedCopy(existing.Id, existing.Name, AlreadyHad: true);

        var includes = new List<Guid>();
        foreach (var nested in shared.Nested)
            includes.Add((await ImportRecipeAsync(nested, context, ct)).Id);

        var lines = shared.Steps.SelectMany(s => s.Ingredients).ToList();
        var matches = await _matcher.MatchSharedAsync(lines
            .Select(l => new SharedIngredientLine(l.ProductName, l.MasterProductId, l.Amount, l.Unit?.Name, l.Unit?.StandardUnit))
            .ToList(), ct);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = ShareText.Required(shared.Name, 200, "Shared recipe"),
            Source = ShareText.TextOrWebUrl(shared.Source, 2000),
            Servings = Math.Max(1, shared.Servings),
            Notes = ShareText.Clean(shared.Notes, 20000),
            Attribution = ShareText.Clean(shared.Attribution, 1000),
            IsMeal = shared.IsMeal,
            OriginKey = originKey,
            SharedByHouseholdName = context.Origin.SharedByHouseholdName
        };

        var line = 0;
        foreach (var sharedStep in shared.Steps)
        {
            var step = new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                StepOrder = recipe.Steps.Count + 1,
                Title = ShareText.Clean(sharedStep.Title, 200),
                Description = ShareText.Clean(sharedStep.Description, 2000),
                Instructions = ShareText.Clean(sharedStep.Instructions, 10000) ?? string.Empty,
                VideoUrl = ShareText.WebUrl(sharedStep.VideoUrl),
                ImageExternalUrl = ShareText.WebUrl(sharedStep.ImageExternalUrl)
            };

            if (context.Image(sharedStep.ImageKey) is { } stepImage)
            {
                var fileName = $"step-image{stepImage.Extension}";
                step.ImageFileName = await SaveAsync(stepImage, s => _storage.SaveRecipeStepImageAsync(recipe.Id, step.Id, s, fileName, ct));
                var stored = step.ImageFileName;
                context.SavedFile(() => _storage.DeleteRecipeStepImageAsync(recipe.Id, step.Id, stored));
                step.ImageOriginalFileName = fileName;
                step.ImageContentType = stepImage.ContentType;
                step.ImageFileSize = stepImage.Bytes.Length;
            }

            foreach (var ingredient in sharedStep.Ingredients)
            {
                var match = matches[line++];
                var linked = match.ProductId.HasValue;
                step.Ingredients.Add(new RecipePosition
                {
                    Id = Guid.NewGuid(),
                    RecipeStepId = step.Id,
                    ProductId = match.ProductId,
                    // A line kept as text is only its text, as an imported one is.
                    Amount = linked ? ingredient.Amount : 0,
                    AmountInGrams = linked ? ingredient.AmountInGrams : 0,
                    QuantityUnitId = linked ? match.QuantityUnitId : null,
                    Note = linked ? ShareText.Clean(ingredient.Note, 500) : null,
                    OriginalText = ShareText.Clean(ingredient.OriginalText ?? Describe(ingredient), 500),
                    IngredientGroup = ShareText.Clean(ingredient.IngredientGroup, 100),
                    OnlyCheckSingleUnitInStock = ingredient.OnlyCheckSingleUnitInStock,
                    NotCheckStockFulfillment = ingredient.NotCheckStockFulfillment,
                    SortOrder = ingredient.SortOrder
                });
            }

            recipe.Steps.Add(step);
        }

        var hasPrimary = false;
        foreach (var sharedImage in shared.Images)
        {
            if (context.Image(sharedImage.Key) is not { } image)
                continue;

            var fileName = $"recipe-image{image.Extension}";
            var stored = await SaveAsync(image, s => _storage.SaveRecipeImageAsync(recipe.Id, s, fileName, ct));
            context.SavedFile(() => _storage.DeleteRecipeImageAsync(recipe.Id, stored));

            var primary = !hasPrimary && sharedImage.IsPrimary;
            hasPrimary |= primary;
            recipe.Images.Add(new RecipeImage
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                FileName = stored,
                OriginalFileName = fileName,
                ContentType = image.ContentType,
                FileSize = image.Bytes.Length,
                SortOrder = recipe.Images.Count,
                IsPrimary = primary,
                ExternalUrl = ShareText.WebUrl(sharedImage.ExternalUrl),
                ExternalThumbnailUrl = ShareText.WebUrl(sharedImage.ExternalThumbnailUrl),
                ExternalSource = ImageExternalSource
            });
        }

        // The sharer's primary picture may have been left out; the first that came across stands in.
        if (!hasPrimary && recipe.Images.FirstOrDefault() is { } first)
            first.IsPrimary = true;

        foreach (var included in includes.Distinct().Where(id => id != recipe.Id))
            recipe.NestedRecipes.Add(new RecipeNesting { Id = Guid.NewGuid(), RecipeId = recipe.Id, IncludesRecipeId = included });

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(ct);

        return new SharedCopy(recipe.Id, recipe.Name, AlreadyHad: false);
    }

    private static async Task<string> SaveAsync(SharedImageBytes image, Func<Stream, Task<string>> save)
    {
        using var stream = new MemoryStream(image.Bytes, writable: false);
        return await save(stream);
    }

    // A line the sharer entered by hand has no text of its own; it is written out from its parts.
    private static string Describe(IngredientSnapshot line) => string.Join(' ', new[]
        {
            line.Amount > 0 ? line.Amount.ToString("0.####") : null,
            line.Unit is { } unit ? (line.Amount == 1 ? unit.Name : unit.NamePlural) : null,
            line.ProductName,
            string.IsNullOrWhiteSpace(line.Note) ? null : $"({line.Note})"
        }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
