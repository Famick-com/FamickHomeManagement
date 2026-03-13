namespace Famick.HomeManagement.Shared.Authentication;

/// <summary>
/// Tenant information for authentication context.
/// Shared between web and mobile clients — single source of truth for subscription state.
/// Mobile uses this directly; web uses Core.DTOs.Authentication.TenantInfoDto (identical shape).
/// </summary>
public class TenantInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = string.Empty;
    public bool IsTrialActive { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool IsExpired { get; set; }
}
