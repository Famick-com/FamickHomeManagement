namespace Famick.HomeManagement.Messaging.Configuration;

/// <summary>
/// Settings for CAN-SPAM and GDPR compliance in notification emails.
/// </summary>
public class ComplianceSettings
{
    public const string SectionName = "Compliance";

    /// <summary>
    /// Company or household name shown in the email footer.
    /// </summary>
    public string CompanyName { get; set; } = "Famick Home Management";

    /// <summary>
    /// Physical mailing address (required by CAN-SPAM).
    /// </summary>
    public string PhysicalAddress { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the unsubscribe endpoint (e.g., "https://app.famick.com").
    /// The full URL is built as: {UnsubscribeBaseUrl}/api/v1/notifications/unsubscribe?token={token}
    /// </summary>
    public string UnsubscribeBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL to the privacy policy page.
    /// </summary>
    public string? PrivacyPolicyUrl { get; set; }
}
