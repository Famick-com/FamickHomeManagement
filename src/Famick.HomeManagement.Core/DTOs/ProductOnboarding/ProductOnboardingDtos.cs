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
    public List<string> DietaryPreferences { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
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
