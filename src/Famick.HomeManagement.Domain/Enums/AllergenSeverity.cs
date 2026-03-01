namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Severity level of an allergen for a household member.
/// </summary>
public enum AllergenSeverity
{
    /// <summary>
    /// Mild sensitivity or intolerance
    /// </summary>
    Sensitivity = 0,

    /// <summary>
    /// True allergy requiring strict avoidance
    /// </summary>
    Allergy = 1
}
