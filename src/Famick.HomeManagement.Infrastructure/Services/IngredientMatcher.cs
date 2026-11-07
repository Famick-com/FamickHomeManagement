using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>An ingredient line and the household product and unit it was matched to, if any.</summary>
public sealed record MatchedIngredientLine(
    string Text,
    ParsedIngredientLine Parsed,
    Guid? ProductId,
    string? ProductName,
    Guid? QuantityUnitId,
    ImportedIngredientOutcome Outcome);

/// <summary>The product a line matched.</summary>
/// <param name="ByDescribedName">
/// Whether the product answered to the name as written ("All-Purpose Flour"), so the line's note
/// needn't repeat the words the product's name already has.
/// </param>
public sealed record IngredientProductMatch(Guid Id, string Name, ImportedIngredientOutcome Outcome, bool ByDescribedName);

/// <summary>
/// Matches ingredient lines to the household's products, or to the catalog's, which are then added to
/// the household. Shared by every way a recipe arrives from outside the household, so they agree on
/// what a line is.
/// </summary>
public class IngredientMatcher
{
    private readonly HomeManagementDbContext _context;
    private readonly IProductsService _productsService;
    private readonly ILogger<IngredientMatcher> _logger;

    public IngredientMatcher(
        HomeManagementDbContext context,
        IProductsService productsService,
        ILogger<IngredientMatcher> logger)
    {
        _context = context;
        _productsService = productsService;
        _logger = logger;
    }

    /// <summary>
    /// Each line matched to a product by an exact, case-insensitive name, singular or plural: the
    /// household's own products first, generic ones first among them, then the catalog's generic
    /// products, which are added to the household. A line without an amount stays text, since a
    /// linked line needs one.
    /// </summary>
    public async Task<List<MatchedIngredientLine>> MatchLinesAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var units = await ActiveUnitsAsync(ct);

        // One lookup per line name, so "flour" twice in a recipe is matched, or adopted, once.
        var matches = new Dictionary<string, IngredientProductMatch?>(StringComparer.OrdinalIgnoreCase);

        var lines = new List<MatchedIngredientLine>();
        foreach (var text in texts)
        {
            var parsed = IngredientLineParser.Parse(text);
            if (parsed.Amount is null || parsed.Name.Length == 0)
            {
                lines.Add(new MatchedIngredientLine(text, parsed, null, null, null, ImportedIngredientOutcome.Unlinked));
                continue;
            }

            var key = parsed.DescribedName + "|" + parsed.Name;
            if (!matches.TryGetValue(key, out var match))
            {
                match = await FindProductAsync(parsed, ct);
                matches[key] = match;
            }

            lines.Add(match is { } product
                ? new MatchedIngredientLine(text, parsed with { Note = product.ByDescribedName ? parsed.NoteWithoutDescription : parsed.Note },
                    product.Id, product.Name, ResolveUnit(parsed, units), product.Outcome)
                : new MatchedIngredientLine(text, parsed, null, null, null, ImportedIngredientOutcome.Unlinked));
        }

        return lines;
    }

    /// <summary>The household's active units, as <see cref="ResolveUnit"/> takes them.</summary>
    public Task<List<QuantityUnit>> ActiveUnitsAsync(CancellationToken ct) => _context.QuantityUnits
        .AsNoTracking()
        .Where(u => u.IsActive)
        .OrderBy(u => u.Name)
        .ToListAsync(ct);

    // A trailing qualifier in parentheses, which a product name can carry: "Eggs (Large)".
    private const string TrailingQualifierPattern = @"\s*\([^)]*\)$";

    // What a recipe calls an ingredient that a store calls something more particular: a recipe's
    // "sugar" is granulated. Tried after the name itself, so a household's own "Sugar" still wins.
    private static readonly Dictionary<string, string> RecipeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sugar"] = "granulated sugar",
        ["white sugar"] = "granulated sugar",
        ["flour"] = "all-purpose flour",
        ["plain flour"] = "all-purpose flour",
        ["confectioners sugar"] = "powdered sugar",
        ["confectioners' sugar"] = "powdered sugar",
        ["icing sugar"] = "powdered sugar",
        ["table salt"] = "salt"
    };

    /// <summary>
    /// The product for a line. Names are tried most specific first: as written ("all-purpose flour"),
    /// then without sizes and preparation ("flour"), each singular or plural, and each against a
    /// product's name with or without a trailing qualifier ("Eggs (Large)" is "eggs"). The household's
    /// products are tried with every name before the catalog's, so a household's "Flour" is used rather
    /// than adding the catalog's "All-Purpose Flour". Among the household's, a generic product comes
    /// before a more specific name: a branded "All-Purpose Flour" doesn't win over a generic "Flour".
    /// </summary>
    public async Task<IngredientProductMatch?> FindProductAsync(ParsedIngredientLine parsed, CancellationToken ct)
    {
        var storeName = IngredientLineParser.NameForms(parsed.Name)
            .Select(form => RecipeNames.GetValueOrDefault(form))
            .FirstOrDefault(name => name != null);

        var candidates = new[] { parsed.DescribedName, parsed.Name, storeName }
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => IngredientLineParser.NameForms(n!))
            .ToList();
        bool ByDescribedName(string productName) => parsed.DescribedName != null && Rank(productName) == 0;
        var forms = candidates.SelectMany(f => f).Distinct().ToList();
        if (forms.Count == 0)
            return null;

        // Which candidate a product's name answers to: lower is more specific.
        int Rank(string productName)
        {
            var lower = productName.ToLowerInvariant();
            var unqualified = Regex.Replace(lower, TrailingQualifierPattern, string.Empty);
            var rank = candidates.FindIndex(c => c.Contains(lower) || c.Contains(unqualified));
            return rank < 0 ? int.MaxValue : rank;
        }

        // Generic, by the same rule as ProductMapper.IsGeneric and the generic-first search.
        var household = (await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive
                            && (forms.Contains(p.Name.ToLower())
                                || forms.Contains(Regex.Replace(p.Name.ToLower(), TrailingQualifierPattern, ""))))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    IsGeneric = p.ChildProducts.Any()
                        || ((p.Brand == null || Regex.IsMatch(p.Brand, ProductSearchService.WhitespaceOnlyPattern))
                            && p.ParentProductId == null)
                })
                .ToListAsync(ct))
            .OrderByDescending(p => p.IsGeneric)
            .ThenBy(p => Rank(p.Name))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (household != null)
            return new IngredientProductMatch(household.Id, household.Name, ImportedIngredientOutcome.LinkedFromHousehold,
                ByDescribedName(household.Name));

        // Only approved, generic catalog products: "All-Purpose Flour", never a branded child of it. The
        // query filter would also let through this household's own pending contributions.
        var master = (await _context.MasterProducts
                .AsNoTracking()
                .Where(m => m.ReviewStatus == MasterProductReviewStatus.Approved
                            && m.ParentMasterProductId == null
                            && (m.Brand == null || Regex.IsMatch(m.Brand, ProductSearchService.WhitespaceOnlyPattern)
                                || m.ChildMasterProducts.Any())
                            && (forms.Contains(m.Name.ToLower())
                                || forms.Contains(Regex.Replace(m.Name.ToLower(), TrailingQualifierPattern, ""))))
                .Select(m => new { m.Id, m.Name, HasChildren = m.ChildMasterProducts.Any() })
                .ToListAsync(ct))
            .OrderBy(m => Rank(m.Name))
            .ThenByDescending(m => m.HasChildren)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (master is not { Id: var id })
            return null;

        try
        {
            var adopted = await _productsService.EnsureProductFromMasterAsync(id, ct);
            return new IngredientProductMatch(adopted.Id, adopted.Name, ImportedIngredientOutcome.AddedFromCatalog,
                ByDescribedName(master.Name));
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
    public static Guid? ResolveUnit(ParsedIngredientLine parsed, IReadOnlyList<QuantityUnit> units)
    {
        if (parsed.StandardUnit is { } standard && units.FirstOrDefault(u => u.StandardUnit == standard) is { } tagged)
            return tagged.Id;

        if (parsed.Unit is not { } written)
            return null;

        var forms = IngredientLineParser.NameForms(written);
        return units.FirstOrDefault(u =>
            forms.Contains(u.Name.ToLowerInvariant()) || forms.Contains(u.NamePlural.ToLowerInvariant()))?.Id;
    }
}
