namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Request to check for duplicate contacts by name
/// </summary>
public class CheckDuplicateContactRequest
{
    /// <summary>
    /// First name to search for
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name to search for (optional)
    /// </summary>
    public string? LastName { get; set; }
}
