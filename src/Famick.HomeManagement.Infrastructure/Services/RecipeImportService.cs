using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Imports a recipe from a web page: fetched through the guarded outbound fetcher, read from its
/// schema.org data by <see cref="RecipePageParser"/>, and saved with each ingredient line matched to a
/// product where one matches exactly.
/// </summary>
public class RecipeImportService : IRecipeImportService
{
    /// <summary>The largest page the importer reads. Recipe pages with all their markup run well under this.</summary>
    public const long MaxPageBytes = 3 * 1024 * 1024;

    /// <summary>The largest photo the importer saves, as for a product image from a URL.</summary>
    public const long MaxImageBytes = ProductsService.MaxImageFromUrlBytes;

    public const string ImageExternalSource = "import";

    /// <summary>
    /// Many recipe sites refuse a request that doesn't look like a browser's, so the page and its photo
    /// are fetched with a browser's headers.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> BrowserHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
        ["Accept-Language"] = "en-US,en;q=0.9"
    };

    private readonly HomeManagementDbContext _context;
    private readonly IOutboundHttpFetcher _fetcher;
    private readonly IProductsService _productsService;
    private readonly IRecipeService _recipeService;
    private readonly ILogger<RecipeImportService> _logger;

    public RecipeImportService(
        HomeManagementDbContext context,
        IOutboundHttpFetcher fetcher,
        IProductsService productsService,
        IRecipeService recipeService,
        ILogger<RecipeImportService> logger)
    {
        _context = context;
        _fetcher = fetcher;
        _productsService = productsService;
        _recipeService = recipeService;
        _logger = logger;
    }

    public async Task<RecipeImportResultDto> ImportAsync(string url, bool allowDuplicate, Guid? userId, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var pageUrl))
            throw new RecipeImportException(RecipeImportException.UrlNotAllowed, "Enter the web address of a recipe page");

        // Before fetching anything: the same link pasted twice is caught without a request.
        if (!allowDuplicate)
            await ThrowIfImportedAsync([pageUrl.AbsoluteUri], ct);

        var page = await FetchPageAsync(pageUrl, ct);
        var imported = RecipePageParser.Parse(page.Body, page.FinalUrl)
            ?? throw new RecipeImportException(RecipeImportException.NoRecipeFound, "No recipe found on that page");

        if (!allowDuplicate)
            await ThrowIfImportedAsync([imported.Source, page.FinalUrl.AbsoluteUri], ct);

        var lines = await MatchIngredientsAsync(imported.Ingredients, ct);
        var recipe = BuildRecipe(imported, lines);

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Imported recipe {RecipeId} from {Host} with {Linked} of {Total} ingredients linked",
            recipe.Id, page.FinalUrl.Host, lines.Count(l => l.Outcome != ImportedIngredientOutcome.Unlinked), lines.Count);

        if (imported.ImageUrl != null)
            await AddPhotoAsync(recipe.Id, imported.ImageUrl, ct);

        var dto = await _recipeService.GetByIdAsync(recipe.Id, userId, ct)
            ?? throw new InvalidOperationException($"Imported recipe {recipe.Id} not found");

        return new RecipeImportResultDto
        {
            Recipe = dto,
            Ingredients = lines.Select(l => new ImportedIngredientDto
            {
                Text = l.Text,
                Outcome = l.Outcome,
                ProductName = l.ProductName
            }).ToList()
        };
    }

    private async Task ThrowIfImportedAsync(IEnumerable<string> sources, CancellationToken ct)
    {
        var candidates = sources.Distinct().ToList();
        var existing = await _context.Recipes
            .Where(r => r.Source != null && candidates.Contains(r.Source))
            .OrderBy(r => r.CreatedAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is { } id)
            throw new RecipeAlreadyImportedException(id);
    }

    private async Task<OutboundFetchResult> FetchPageAsync(Uri pageUrl, CancellationToken ct)
    {
        if (_fetcher.CheckUrl(pageUrl) != null)
            throw new RecipeImportException(RecipeImportException.UrlNotAllowed, "That address can't be imported from");

        OutboundFetchResult page;
        try
        {
            page = await _fetcher.GetAsync(pageUrl, MaxPageBytes, BrowserHeaders, ct);
        }
        catch (OutboundFetchException ex)
        {
            _logger.LogInformation("Recipe import fetch from {Host} failed: {Failure} {Status}",
                pageUrl.Host, ex.Failure, ex.StatusCode);

            throw ex.Failure switch
            {
                OutboundFetchFailure.UrlNotAllowed or OutboundFetchFailure.AddressNotAllowed =>
                    new RecipeImportException(RecipeImportException.UrlNotAllowed, "That address can't be imported from"),
                OutboundFetchFailure.TooLarge =>
                    new RecipeImportException(RecipeImportException.PageTooLarge, "That page is too large to import"),
                _ => new RecipeImportException(RecipeImportException.PageUnreachable, "Couldn't open that page")
            };
        }

        // A missing Content-Type is read as HTML; a declared one must say so.
        var mediaType = page.ContentType?.Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(mediaType)
            && !mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            throw new RecipeImportException(RecipeImportException.NotAWebPage, "That address isn't a web page");

        return page;
    }

    private sealed record MatchedLine(
        string Text,
        ParsedIngredientLine Parsed,
        Guid? ProductId,
        string? ProductName,
        Guid? QuantityUnitId,
        ImportedIngredientOutcome Outcome);

    /// <summary>
    /// Each line matched to a product by an exact, case-insensitive name, singular or plural: the
    /// household's own products first, generic ones first among them, then the catalog's generic
    /// products, which are added to the household. A line without an amount stays text, since a
    /// linked line needs one.
    /// </summary>
    private async Task<List<MatchedLine>> MatchIngredientsAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var units = await _context.QuantityUnits
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);

        // One lookup per name, so "flour" twice in a recipe is matched, or adopted, once.
        var matches = new Dictionary<string, (Guid Id, string Name, ImportedIngredientOutcome Outcome)?>(StringComparer.OrdinalIgnoreCase);

        var lines = new List<MatchedLine>();
        foreach (var text in texts)
        {
            var parsed = IngredientLineParser.Parse(text);
            if (parsed.Amount is null || parsed.Name.Length == 0)
            {
                lines.Add(new MatchedLine(text, parsed, null, null, null, ImportedIngredientOutcome.Unlinked));
                continue;
            }

            if (!matches.TryGetValue(parsed.Name, out var match))
            {
                match = await FindProductAsync(parsed.Name, ct);
                matches[parsed.Name] = match;
            }

            lines.Add(match is { } product
                ? new MatchedLine(text, parsed, product.Id, product.Name, ResolveUnit(parsed, units), product.Outcome)
                : new MatchedLine(text, parsed, null, null, null, ImportedIngredientOutcome.Unlinked));
        }

        return lines;
    }

    private async Task<(Guid Id, string Name, ImportedIngredientOutcome Outcome)?> FindProductAsync(string name, CancellationToken ct)
    {
        var forms = IngredientLineParser.NameForms(name).ToList();
        if (forms.Count == 0)
            return null;

        // Generic first, by the same rule as ProductMapper.IsGeneric and the generic-first search.
        var household = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && forms.Contains(p.Name.ToLower()))
            .OrderByDescending(p => p.ChildProducts.Any()
                || ((p.Brand == null || Regex.IsMatch(p.Brand, ProductSearchService.WhitespaceOnlyPattern))
                    && p.ParentProductId == null))
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);

        if (household != null)
            return (household.Id, household.Name, ImportedIngredientOutcome.LinkedFromHousehold);

        // Only approved, generic catalog products: "Flour", never a branded child of it. The query
        // filter would also let through this household's own pending contributions.
        var masterId = await _context.MasterProducts
            .AsNoTracking()
            .Where(m => m.ReviewStatus == MasterProductReviewStatus.Approved
                        && m.ParentMasterProductId == null
                        && (m.Brand == null || Regex.IsMatch(m.Brand, ProductSearchService.WhitespaceOnlyPattern)
                            || m.ChildMasterProducts.Any())
                        && forms.Contains(m.Name.ToLower()))
            .OrderByDescending(m => m.ChildMasterProducts.Any())
            .ThenBy(m => m.Name)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        if (masterId is not { } id)
            return null;

        try
        {
            var adopted = await _productsService.EnsureProductFromMasterAsync(id, ct);
            return (adopted.Id, adopted.Name, ImportedIngredientOutcome.AddedFromCatalog);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            // A household with no location or unit yet can't take a product; the line stays text.
            _logger.LogWarning(ex, "Couldn't add catalog product {MasterProductId} for an imported ingredient", id);
            return null;
        }
    }

    /// <summary>
    /// The household unit for the line's unit: one tagged with the same standard measure, else one named
    /// the same (singular or plural), else none.
    /// </summary>
    private static Guid? ResolveUnit(ParsedIngredientLine parsed, List<QuantityUnit> units)
    {
        if (parsed.StandardUnit is { } standard && units.FirstOrDefault(u => u.StandardUnit == standard) is { } tagged)
            return tagged.Id;

        if (parsed.Unit is not { } written)
            return null;

        var forms = IngredientLineParser.NameForms(written);
        return units.FirstOrDefault(u =>
            forms.Contains(u.Name.ToLowerInvariant()) || forms.Contains(u.NamePlural.ToLowerInvariant()))?.Id;
    }

    private static Recipe BuildRecipe(ImportedRecipe imported, List<MatchedLine> lines)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = imported.Name,
            Servings = imported.Servings,
            Notes = imported.Description,
            Source = imported.Source,
            Attribution = imported.Attribution
        };

        // Ingredients belong to a step, and the page doesn't say which, so they all go on the first.
        var steps = imported.Steps.Count > 0
            ? imported.Steps
            : [new ImportedRecipeStep(null, string.Empty)];

        for (var i = 0; i < steps.Count; i++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                StepOrder = i + 1,
                Title = steps[i].Title,
                Instructions = steps[i].Instructions
            });
        }

        var first = recipe.Steps.First();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var linked = line.ProductId.HasValue;
            first.Ingredients.Add(new RecipePosition
            {
                Id = Guid.NewGuid(),
                RecipeStepId = first.Id,
                ProductId = line.ProductId,
                Amount = linked ? line.Parsed.Amount!.Value : 0,
                QuantityUnitId = linked ? line.QuantityUnitId : null,
                Note = linked ? Truncate(line.Parsed.Note, 500) : null,
                OriginalText = line.Text,
                SortOrder = i
            });
        }

        return recipe;
    }

    /// <summary>
    /// The page's photo as the recipe's primary image. Best effort: a photo that can't be fetched, or isn't
    /// a JPEG, PNG or WebP, leaves the recipe without one.
    /// </summary>
    private async Task AddPhotoAsync(Guid recipeId, string imageUrl, CancellationToken ct)
    {
        try
        {
            var fetched = await _fetcher.GetAsync(new Uri(imageUrl), MaxImageBytes, BrowserHeaders, ct);

            // Trust the bytes, not the remote Content-Type or the URL's file name.
            var format = ImageSignature.Detect(fetched.Body);
            if (format == null || format.ContentType == "image/gif")
            {
                _logger.LogInformation("Skipped imported recipe photo for {RecipeId}: not JPEG, PNG or WebP", recipeId);
                return;
            }

            using var stream = new MemoryStream(fetched.Body, writable: false);
            var image = await _recipeService.AddImageAsync(recipeId, stream, $"recipe-image{format.Extension}",
                format.ContentType, fetched.Body.Length, ct);

            // Only the source is recorded. An ExternalUrl would be served in place of the stored copy, and
            // the clients would load the photo from the recipe site.
            var entity = await _context.Set<RecipeImage>().FirstAsync(i => i.Id == image.Id, ct);
            entity.ExternalSource = ImageExternalSource;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Couldn't add the imported photo to recipe {RecipeId}", recipeId);
        }
    }

    private static string? Truncate(string? text, int length) =>
        text == null || text.Length <= length ? text : text[..length];
}
