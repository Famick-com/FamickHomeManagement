using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.ProductOnboarding;

/// <summary>
/// Response for GET /api/v1/product-onboarding (tenant-level state).
/// </summary>
public class ProductOnboardingStateDto
{
    public bool HasCompletedOnboarding { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProductsCreatedCount { get; set; }
    public ProductOnboardingAnswersDto? SavedAnswers { get; set; }
}

/// <summary>
/// Questionnaire answers shared across all wizard steps.
/// Serialized to JSON and stored on TenantProductOnboardingState for re-run pre-population.
/// </summary>
public class ProductOnboardingAnswersDto
{
    public bool HasBaby { get; set; }
    public bool HasPets { get; set; }
    public bool TrackHouseholdSupplies { get; set; }
    public bool TrackPersonalCare { get; set; }
    public bool TrackPharmacy { get; set; }
    public List<DietaryPreference> DietaryPreferences { get; set; } = new();
    public List<AllergenType> Allergens { get; set; } = new();
    public List<CookingStyleInterest> CookingStyles { get; set; } = new();
}

/// <summary>
/// Response for POST /api/v1/product-onboarding/preview.
/// </summary>
public class ProductOnboardingPreviewResponse
{
    public int TotalMasterProducts { get; set; }
    public int FilteredCount { get; set; }
    public List<MasterProductCategoryGroup> Categories { get; set; } = new();
}

/// <summary>
/// A group of master products sharing the same category.
/// </summary>
public class MasterProductCategoryGroup
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public List<MasterProductDto> Items { get; set; } = new();
}

/// <summary>
/// A single master product in the preview response.
/// </summary>
public class MasterProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ContainerType { get; set; }
    public bool IsStaple { get; set; }
}

/// <summary>
/// Request for POST /api/v1/product-onboarding/complete.
/// </summary>
public class ProductOnboardingCompleteRequest
{
    public ProductOnboardingAnswersDto Answers { get; set; } = new();
    public List<Guid> SelectedMasterProductIds { get; set; } = new();
}

/// <summary>
/// Response for POST /api/v1/product-onboarding/complete.
/// </summary>
public class ProductOnboardingCompleteResponse
{
    public int ProductsCreated { get; set; }
    public int ProductsSkipped { get; set; }
}
