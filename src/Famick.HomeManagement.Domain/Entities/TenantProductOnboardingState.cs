namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks product onboarding completion state per tenant (household).
/// One record per tenant. Shared by all household members.
/// </summary>
public class TenantProductOnboardingState : BaseTenantEntity
{
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// Serialized questionnaire answers (JSON) for re-run pre-population.
    /// </summary>
    public string? QuestionnaireAnswersJson { get; set; }

    public DateTime? CompletedAt { get; set; }
    public int ProductsCreatedCount { get; set; }
}
