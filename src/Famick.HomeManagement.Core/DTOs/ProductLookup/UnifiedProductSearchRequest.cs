namespace Famick.HomeManagement.Core.DTOs.ProductLookup;

/// <summary>
/// The situation a product search is running in. Determines which sources are queried
/// and how results are ranked.
/// </summary>
public enum ProductSearchContext
{
    /// <summary>
    /// Generic search (recipes, product linking, admin, etc.). Sources: local + master
    /// catalog, ranked purely by relevance. External sources (plugins/store) are only
    /// queried when <see cref="UnifiedProductSearchRequest.IncludeExternal"/> is set.
    /// </summary>
    General = 0,

    /// <summary>
    /// Building a shopping list. Sources: master catalog + local products + the list's
    /// linked store integration. Ranked master → local → store.
    /// </summary>
    ShoppingList = 1
}

/// <summary>
/// Single request contract for the unified product search. Replaces the fragmented
/// autocomplete / parent-search / lookup / store-search requests.
/// </summary>
public class UnifiedProductSearchRequest
{
    /// <summary>
    /// Search query — a barcode (8-14 digits) or a product name. The system auto-detects
    /// which kind of search to perform.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return (default: 20).
    /// </summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// The context this search runs in (see <see cref="ProductSearchContext"/>).
    /// </summary>
    public ProductSearchContext Context { get; set; } = ProductSearchContext.General;

    /// <summary>
    /// Shopping list being edited (ShoppingList context). Used to resolve the linked store.
    /// </summary>
    public Guid? ShoppingListId { get; set; }

    /// <summary>
    /// Store to include in the search. In ShoppingList context this is the list's linked
    /// store; in General context it is only queried when <see cref="IncludeExternal"/> is set.
    /// </summary>
    public Guid? ShoppingLocationId { get; set; }

    /// <summary>
    /// General context only: opt in to external sources (USDA/OpenFoodFacts and the store,
    /// if any). Never automatic — surfaced as an explicit "search external" affordance.
    /// Ignored in ShoppingList context, where the store is always included.
    /// </summary>
    public bool IncludeExternal { get; set; }

    /// <summary>
    /// Optional product to exclude from results (e.g. a product cannot be its own parent
    /// on the parent-linking screen).
    /// </summary>
    public Guid? ExcludeProductId { get; set; }

    // ── Advanced source overrides ──
    // Normal callers leave these null and let Context/IncludeExternal decide. The legacy
    // endpoint shims set them explicitly to reproduce their original source sets faithfully
    // (e.g. "external sources only" excludes local + master). Null = context default.

    /// <summary>Override whether local tenant products are searched. Null = context default (true).</summary>
    public bool? IncludeLocal { get; set; }

    /// <summary>Override whether the master catalog is searched. Null = context default (true).</summary>
    public bool? IncludeMaster { get; set; }

    /// <summary>Override whether the linked store is searched. Null = context default.</summary>
    public bool? IncludeStore { get; set; }

    /// <summary>Override whether external plugins (USDA/OpenFoodFacts) are searched. Null = context default.</summary>
    public bool? IncludePlugins { get; set; }
}
