namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Request to create or update the current user's contact record
/// </summary>
public class SaveCurrentUserContactRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}
