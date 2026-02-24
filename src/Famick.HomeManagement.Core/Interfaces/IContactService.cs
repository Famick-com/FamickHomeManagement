using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing contacts, relationships, and related data
/// </summary>
public interface IContactService
{
    #region Contact CRUD

    /// <summary>
    /// Creates a new contact
    /// </summary>
    Task<ContactDto> CreateAsync(CreateContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a contact for a newly registered user
    /// </summary>
    Task<ContactDto> CreateContactForUserAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Gets contact by ID with full details
    /// </summary>
    Task<ContactDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a contact by linked user ID
    /// </summary>
    Task<ContactDto?> GetByLinkedUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lists contacts with filtering and pagination
    /// </summary>
    Task<PagedResult<ContactSummaryDto>> ListAsync(ContactFilterRequest filter, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing contact
    /// </summary>
    Task<ContactDto> UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a contact (removes user link if exists)
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Searches contacts by name, email, or phone
    /// </summary>
    Task<List<ContactSummaryDto>> SearchAsync(string searchTerm, int limit = 10, CancellationToken ct = default);

    #endregion

    #region Contact Groups

    /// <summary>
    /// Creates a new contact group (Household or Business)
    /// </summary>
    Task<ContactGroupSummaryDto> CreateGroupAsync(CreateContactGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a group by ID with members
    /// </summary>
    Task<ContactDto> GetGroupByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists contact groups with filtering and pagination
    /// </summary>
    Task<PagedResult<ContactGroupSummaryDto>> ListGroupsAsync(ContactFilterRequest filter, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing contact group
    /// </summary>
    Task UpdateGroupAsync(Guid id, UpdateContactGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a contact group (moves members to tenant household)
    /// </summary>
    Task DeleteGroupAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Moves a contact to a different group
    /// </summary>
    Task MoveContactToGroupAsync(Guid contactId, Guid targetGroupId, CancellationToken ct = default);

    /// <summary>
    /// Gets the tenant's household group
    /// </summary>
    Task<ContactDto> GetTenantHouseholdAsync(CancellationToken ct = default);

    /// <summary>
    /// Ensures a tenant household group exists, creating one if necessary
    /// </summary>
    Task<ContactDto> EnsureTenantHouseholdAsync(string householdName, CancellationToken ct = default);

    #endregion

    #region Address Management

    /// <summary>
    /// Adds an address to a contact
    /// </summary>
    Task<ContactAddressDto> AddAddressAsync(Guid contactId, AddContactAddressRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes an address from a contact
    /// </summary>
    Task RemoveAddressAsync(Guid contactId, Guid contactAddressId, CancellationToken ct = default);

    /// <summary>
    /// Sets an address as the primary address for a contact
    /// </summary>
    Task SetPrimaryAddressAsync(Guid contactId, Guid contactAddressId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing address for a contact
    /// </summary>
    Task<ContactAddressDto> UpdateAddressAsync(Guid contactId, Guid contactAddressId, AddContactAddressRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets all addresses for a contact
    /// </summary>
    Task<List<ContactAddressDto>> GetAddressesAsync(Guid contactId, CancellationToken ct = default);

    #endregion

    #region Phone Management

    /// <summary>
    /// Adds a phone number to a contact
    /// </summary>
    Task<ContactPhoneNumberDto> AddPhoneAsync(Guid contactId, AddPhoneRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates a phone number
    /// </summary>
    Task<ContactPhoneNumberDto> UpdatePhoneAsync(Guid phoneId, AddPhoneRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a phone number from a contact
    /// </summary>
    Task RemovePhoneAsync(Guid phoneId, CancellationToken ct = default);

    /// <summary>
    /// Sets a phone as the primary phone for a contact
    /// </summary>
    Task SetPrimaryPhoneAsync(Guid contactId, Guid phoneId, CancellationToken ct = default);

    #endregion

    #region Email Management

    /// <summary>
    /// Adds an email address to a contact
    /// </summary>
    Task<ContactEmailAddressDto> AddEmailAsync(Guid contactId, AddEmailRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an email address
    /// </summary>
    Task<ContactEmailAddressDto> UpdateEmailAsync(Guid emailId, AddEmailRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes an email address from a contact
    /// </summary>
    Task RemoveEmailAsync(Guid emailId, CancellationToken ct = default);

    /// <summary>
    /// Sets an email as the primary email for a contact
    /// </summary>
    Task SetPrimaryEmailAsync(Guid contactId, Guid emailId, CancellationToken ct = default);

    #endregion

    #region Social Media Management

    /// <summary>
    /// Adds a social media profile to a contact
    /// </summary>
    Task<ContactSocialMediaDto> AddSocialMediaAsync(Guid contactId, AddSocialMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates a social media profile
    /// </summary>
    Task<ContactSocialMediaDto> UpdateSocialMediaAsync(Guid socialMediaId, AddSocialMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a social media profile from a contact
    /// </summary>
    Task RemoveSocialMediaAsync(Guid socialMediaId, CancellationToken ct = default);

    #endregion

    #region Relationship Management

    /// <summary>
    /// Adds a relationship between contacts (optionally creates inverse)
    /// </summary>
    Task<ContactRelationshipDto> AddRelationshipAsync(Guid sourceContactId, AddRelationshipRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a relationship (optionally removes inverse)
    /// </summary>
    Task RemoveRelationshipAsync(Guid relationshipId, bool removeInverse = true, CancellationToken ct = default);

    /// <summary>
    /// Gets all relationships for a contact
    /// </summary>
    Task<List<ContactRelationshipDto>> GetRelationshipsAsync(Guid contactId, CancellationToken ct = default);

    /// <summary>
    /// Gets contacts that have a relationship to the specified contact
    /// </summary>
    Task<List<ContactRelationshipDto>> GetInverseRelationshipsAsync(Guid contactId, CancellationToken ct = default);

    #endregion

    #region Tag Management

    /// <summary>
    /// Creates a new contact tag
    /// </summary>
    Task<ContactTagDto> CreateTagAsync(CreateContactTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a tag by ID
    /// </summary>
    Task<ContactTagDto?> GetTagByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all contact tags
    /// </summary>
    Task<List<ContactTagDto>> ListTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates a contact tag
    /// </summary>
    Task<ContactTagDto> UpdateTagAsync(Guid id, UpdateContactTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a contact tag
    /// </summary>
    Task DeleteTagAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Adds a tag to a contact
    /// </summary>
    Task AddTagToContactAsync(Guid contactId, Guid tagId, CancellationToken ct = default);

    /// <summary>
    /// Removes a tag from a contact
    /// </summary>
    Task RemoveTagFromContactAsync(Guid contactId, Guid tagId, CancellationToken ct = default);

    /// <summary>
    /// Updates the tags for a contact (replaces all)
    /// </summary>
    Task SetContactTagsAsync(Guid contactId, List<Guid> tagIds, CancellationToken ct = default);

    #endregion

    #region Sharing Management

    /// <summary>
    /// Shares a contact with another user
    /// </summary>
    Task<ContactUserShareDto> ShareContactAsync(Guid contactId, ShareContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates sharing permissions
    /// </summary>
    Task<ContactUserShareDto> UpdateShareAsync(Guid shareId, bool canEdit, CancellationToken ct = default);

    /// <summary>
    /// Removes sharing for a contact
    /// </summary>
    Task RemoveShareAsync(Guid shareId, CancellationToken ct = default);

    /// <summary>
    /// Gets sharing info for a contact
    /// </summary>
    Task<List<ContactUserShareDto>> GetSharesAsync(Guid contactId, CancellationToken ct = default);

    #endregion

    #region Profile Image Management

    /// <summary>
    /// Uploads a profile image for a contact (replaces existing)
    /// </summary>
    Task<string> UploadProfileImageAsync(Guid contactId, Stream imageStream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes the profile image for a contact
    /// </summary>
    Task DeleteProfileImageAsync(Guid contactId, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for a contact's profile image
    /// </summary>
    string? GetProfileImageUrl(Guid contactId);

    #endregion

    #region Audit Log

    /// <summary>
    /// Gets audit log entries for a contact
    /// </summary>
    Task<List<ContactAuditLogDto>> GetAuditLogAsync(Guid contactId, int? limit = null, CancellationToken ct = default);

    #endregion
}

/// <summary>
/// Paged result container
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
