namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact sharing information
/// </summary>
public class ContactUserShareDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid SharedWithUserId { get; set; }

    /// <summary>
    /// Name of the user the contact is shared with
    /// </summary>
    public string SharedWithUserName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the shared user can edit the contact
    /// </summary>
    public bool CanEdit { get; set; }

    public DateTime CreatedAt { get; set; }
}
