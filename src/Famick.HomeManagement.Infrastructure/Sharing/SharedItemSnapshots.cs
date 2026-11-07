using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// One kind of shareable item: how it is read in the sharing household and copied into another.
/// Called only by <see cref="SharedItemCopier"/>, which runs each half in its household's scope.
/// </summary>
public interface IShareableItemHandler
{
    ShareableKind Kind { get; }

    /// <summary>
    /// The item as the current member of the current household sees it, or null when it isn't there for
    /// them: gone, inactive or not visible to them.
    /// </summary>
    Task<SharedItemSnapshot?> SnapshotAsync(Guid itemId, ShareSnapshotContext context, CancellationToken ct);

    /// <summary>Adds a copy to the current household, or finds the one it already has.</summary>
    Task<SharedCopy> ImportAsync(SharedItemSnapshot snapshot, ShareImportContext context, CancellationToken ct);

    SharedItemPreview Preview(SharedItemSnapshot snapshot, string sharedByHouseholdName, bool hasImage);
}

/// <summary>The copy an import made or found.</summary>
public sealed record SharedCopy(Guid Id, string Name, bool AlreadyHad);

/// <summary>
/// A shared item as read in the sharing household. It stays in the copier: it holds the source's id,
/// for the origin key, and no shape that leaves the server is made from it except a preview.
/// </summary>
public abstract class SharedItemSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public abstract ShareableKind Kind { get; }

    public Guid SourceId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The key of the picture a preview shows, if any.</summary>
    public abstract string? PreviewImageKey { get; }
}

/// <summary>A picture: read bytes by key, or only an address when the source stored none.</summary>
public sealed record SharedImageSnapshot(string? Key, string? ExternalUrl, string? ExternalThumbnailUrl, bool IsPrimary);

public sealed class RecipeSnapshot : SharedItemSnapshot
{
    public override ShareableKind Kind => ShareableKind.Recipe;
    public override string? PreviewImageKey => Images.FirstOrDefault(i => i.IsPrimary)?.Key ?? Images.FirstOrDefault()?.Key;

    public string? Source { get; init; }
    public int Servings { get; init; }
    public string? Notes { get; init; }
    public string? Attribution { get; init; }
    public bool IsMeal { get; init; }
    public List<SharedImageSnapshot> Images { get; init; } = new();
    public List<RecipeStepSnapshot> Steps { get; init; } = new();
    public List<RecipeSnapshot> Nested { get; init; } = new();
}

public sealed class RecipeStepSnapshot
{
    public int StepOrder { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string Instructions { get; init; } = string.Empty;
    public string? VideoUrl { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageExternalUrl { get; init; }
    public List<IngredientSnapshot> Ingredients { get; init; } = new();
}

/// <summary>An ingredient line, by what it names rather than the sharing household's product.</summary>
public sealed class IngredientSnapshot
{
    public string? OriginalText { get; init; }
    public string? ProductName { get; init; }

    /// <summary>The catalog product behind the line's product, when the catalog has published it.</summary>
    public Guid? MasterProductId { get; init; }

    public decimal Amount { get; init; }
    public decimal AmountInGrams { get; init; }
    public SharedUnit? Unit { get; init; }
    public string? Note { get; init; }
    public string? IngredientGroup { get; init; }
    public bool OnlyCheckSingleUnitInStock { get; init; }
    public bool NotCheckStockFulfillment { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>A unit, by name and standard measure, matched to the receiving household's own.</summary>
public sealed record SharedUnit(string Name, string NamePlural, StandardUnit? StandardUnit);

public sealed class ProductSnapshot : SharedItemSnapshot
{
    public override ShareableKind Kind => ShareableKind.Product;
    public override string? PreviewImageKey => Images.FirstOrDefault(i => i.IsPrimary)?.Key ?? Images.FirstOrDefault()?.Key;

    public string? Description { get; init; }
    public string? Brand { get; init; }
    public ProductSaleType SaleType { get; init; }
    public int DefaultBestBeforeDays { get; init; }
    public bool TracksBestBeforeDate { get; init; }
    public int? ExpiryWarningDays { get; init; }
    public decimal? ServingSize { get; init; }
    public string? ServingUnit { get; init; }
    public decimal? ServingsPerContainer { get; init; }
    public string? DataSourceAttribution { get; init; }

    /// <summary>The catalog product it follows, when the catalog has published it; else none.</summary>
    public Guid? MasterProductId { get; init; }

    /// <summary>The fields it sets for itself over the catalog's; only with <see cref="MasterProductId"/>.</summary>
    public string? OverriddenFields { get; init; }

    public string? GroupName { get; init; }
    public SharedUnit PurchaseUnit { get; init; } = new("Piece", "Pieces", null);
    public SharedUnit StockUnit { get; init; } = new("Piece", "Pieces", null);
    public decimal PurchaseToStockFactor { get; init; } = 1m;
    public List<SharedBarcode> Barcodes { get; init; } = new();

    /// <summary>A detached copy of the source's nutrition, written anew for the copy.</summary>
    public ProductNutrition? Nutrition { get; init; }

    public List<AllergenType> Allergens { get; init; } = new();
    public List<DietaryPreference> DietaryConflicts { get; init; } = new();
    public List<SharedImageSnapshot> Images { get; init; } = new();
}

public sealed record SharedBarcode(string Barcode, string? Note, string? Type2Prefix);

public sealed class MealSnapshot : SharedItemSnapshot
{
    public override ShareableKind Kind => ShareableKind.Meal;
    public override string? PreviewImageKey =>
        Items.Select(i => i.Recipe?.PreviewImageKey ?? i.Product?.PreviewImageKey).FirstOrDefault(k => k != null);

    public string? Notes { get; init; }
    public List<MealItemSnapshot> Items { get; init; } = new();
}

public sealed class MealItemSnapshot
{
    public MealItemType ItemType { get; init; }
    public RecipeSnapshot? Recipe { get; init; }
    public ProductSnapshot? Product { get; init; }
    public decimal? ProductQuantity { get; init; }
    public SharedUnit? ProductQuantityUnit { get; init; }
    public string? FreetextDescription { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ContactSnapshot : SharedItemSnapshot
{
    public override ShareableKind Kind => ShareableKind.Contact;
    public override string? PreviewImageKey => PhotoKey;

    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? PreferredName { get; init; }
    public string? CompanyName { get; init; }
    public string? Title { get; init; }
    public ContactType? ContactType { get; init; }
    public string? BusinessCategory { get; init; }
    public Gender Gender { get; init; }
    public int? BirthYear { get; init; }
    public int? BirthMonth { get; init; }
    public int? BirthDay { get; init; }
    public DatePrecision BirthDatePrecision { get; init; }
    public string? Website { get; init; }
    public bool UseGravatar { get; init; }
    public string? PhotoKey { get; init; }
    public List<SharedEmail> Emails { get; init; } = new();
    public List<SharedPhone> Phones { get; init; } = new();
    public List<SharedAddress> Addresses { get; init; } = new();
    public List<SharedSocialMedia> SocialMedia { get; init; } = new();
}

public sealed record SharedEmail(string Email, EmailTag Tag, bool IsPrimary, string? Label);
public sealed record SharedPhone(string PhoneNumber, PhoneTag Tag, bool IsPrimary);

/// <summary>
/// An address, by its row in the address book every household shares: rows are never edited, so the
/// copy links the same one.
/// </summary>
public sealed record SharedAddress(Guid AddressId, AddressTag Tag, bool IsPrimary, string? AddressLine2, string? AddressLine3, string? AddressLine4);

public sealed record SharedSocialMedia(SocialMediaService Service, string Username, string? ProfileUrl);
