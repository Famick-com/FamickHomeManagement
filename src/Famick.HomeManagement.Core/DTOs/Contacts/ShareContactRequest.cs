namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to share a contact with another user
/// </summary>
public class ShareContactRequest
{
    /// <summary>
    /// User ID to share with
    /// </summary>
    public Guid SharedWithUserId { get; set; }

    /// <summary>
    /// Whether the shared user can edit the contact
    /// </summary>
    public bool CanEdit { get; set; } = false;
}
