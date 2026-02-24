using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing contacts and related data
/// </summary>
[ApiController]
[Route("api/v1/contacts")]
[Authorize]
public class ContactsController : ApiControllerBase
{
    private readonly IContactService _contactService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFileAccessTokenService _tokenService;

    public ContactsController(
        IContactService contactService,
        IFileStorageService fileStorageService,
        IFileAccessTokenService tokenService,
        ITenantProvider tenantProvider,
        ILogger<ContactsController> logger)
        : base(tenantProvider, logger)
    {
        _contactService = contactService;
        _fileStorageService = fileStorageService;
        _tokenService = tokenService;
    }

    #region Contact CRUD

    /// <summary>
    /// Gets a list of contacts with optional filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContactSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(
        [FromQuery] ContactFilterRequest filter,
        CancellationToken ct)
    {
        _logger.LogInformation("Listing contacts for tenant {TenantId}", TenantId);

        var result = await _contactService.ListAsync(filter, ct);

        return ApiResponse(result);
    }

    /// <summary>
    /// Searches contacts by name, email, or phone
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<ContactSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Search(
        [FromQuery] string searchTerm,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Searching contacts for term '{Term}' in tenant {TenantId}", searchTerm, TenantId);

        var result = await _contactService.SearchAsync(searchTerm, limit, ct);

        return ApiResponse(result);
    }

    /// <summary>
    /// Gets a single contact by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting contact {ContactId} for tenant {TenantId}", id, TenantId);

        var contact = await _contactService.GetByIdAsync(id, ct);

        if (contact == null)
        {
            return NotFoundResponse("Contact not found");
        }

        return ApiResponse(contact);
    }

    /// <summary>
    /// Gets the current user's contact record
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyContact(CancellationToken ct)
    {
        var userId = _tenantProvider.UserId;
        if (!userId.HasValue)
        {
            return UnauthorizedResponse();
        }

        _logger.LogInformation("Getting contact for user {UserId}", userId);

        var contact = await _contactService.GetByLinkedUserIdAsync(userId.Value, ct);

        if (contact == null)
        {
            return NotFoundResponse("Contact not found for current user");
        }

        return ApiResponse(contact);
    }

    /// <summary>
    /// Creates a new contact
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContactRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating contact '{FirstName} {LastName}' for tenant {TenantId}",
            request.FirstName, request.LastName, TenantId);

        var contact = await _contactService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = contact.Id }, contact);
    }

    /// <summary>
    /// Updates an existing contact
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateContactRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating contact {ContactId} for tenant {TenantId}", id, TenantId);

        var contact = await _contactService.UpdateAsync(id, request, ct);

        return ApiResponse(contact);
    }

    /// <summary>
    /// Deletes a contact
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting contact {ContactId} for tenant {TenantId}", id, TenantId);

        await _contactService.DeleteAsync(id, ct);

        return NoContent();
    }

    #endregion

    #region Contact Groups

    /// <summary>
    /// Gets a list of contact groups with optional filtering and pagination
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(PagedResult<ContactGroupSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ListGroups(
        [FromQuery] ContactFilterRequest filter,
        CancellationToken ct)
    {
        _logger.LogInformation("Listing contact groups for tenant {TenantId}", TenantId);

        var result = await _contactService.ListGroupsAsync(filter, ct);

        return ApiResponse(result);
    }

    /// <summary>
    /// Gets a contact group by ID with its members
    /// </summary>
    [HttpGet("groups/{id}")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGroup(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting contact group {GroupId} for tenant {TenantId}", id, TenantId);

        try
        {
            var group = await _contactService.GetGroupByIdAsync(id, ct);
            return ApiResponse(group);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Creates a new contact group
    /// </summary>
    [HttpPost("groups")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactGroupSummaryDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateGroup(
        [FromBody] CreateContactGroupRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating contact group '{GroupName}' for tenant {TenantId}",
            request.GroupName, TenantId);

        var group = await _contactService.CreateGroupAsync(request, ct);

        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
    }

    /// <summary>
    /// Updates an existing contact group
    /// </summary>
    [HttpPut("groups/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateGroup(
        Guid id,
        [FromBody] UpdateContactGroupRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating contact group {GroupId} for tenant {TenantId}", id, TenantId);

        try
        {
            await _contactService.UpdateGroupAsync(id, request, ct);
            return NoContent();
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a contact group (members are moved to tenant household)
    /// </summary>
    [HttpDelete("groups/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting contact group {GroupId} for tenant {TenantId}", id, TenantId);

        try
        {
            await _contactService.DeleteGroupAsync(id, ct);
            return NoContent();
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Gets the tenant's household group
    /// </summary>
    [HttpGet("groups/my-household")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyHousehold(CancellationToken ct)
    {
        _logger.LogInformation("Getting tenant household for tenant {TenantId}", TenantId);

        try
        {
            var household = await _contactService.GetTenantHouseholdAsync(ct);
            return ApiResponse(household);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Moves a contact to a different group
    /// </summary>
    [HttpPost("{id}/move-to-group/{groupId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MoveToGroup(Guid id, Guid groupId, CancellationToken ct)
    {
        _logger.LogInformation("Moving contact {ContactId} to group {GroupId} for tenant {TenantId}",
            id, groupId, TenantId);

        try
        {
            await _contactService.MoveContactToGroupAsync(id, groupId, ct);
            return NoContent();
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    #endregion

    #region Addresses

    /// <summary>
    /// Gets all addresses for a contact
    /// </summary>
    [HttpGet("{id}/addresses")]
    [ProducesResponseType(typeof(List<ContactAddressDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAddresses(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting addresses for contact {ContactId}", id);

        var addresses = await _contactService.GetAddressesAsync(id, ct);

        return ApiResponse(addresses);
    }

    /// <summary>
    /// Adds an address to a contact
    /// </summary>
    [HttpPost("{id}/addresses")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactAddressDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddAddress(
        Guid id,
        [FromBody] AddContactAddressRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding address to contact {ContactId}", id);

        var address = await _contactService.AddAddressAsync(id, request, ct);

        return CreatedAtAction(nameof(GetAddresses), new { id }, address);
    }

    /// <summary>
    /// Removes an address from a contact
    /// </summary>
    [HttpDelete("{id}/addresses/{addressId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveAddress(Guid id, Guid addressId, CancellationToken ct)
    {
        _logger.LogInformation("Removing address {AddressId} from contact {ContactId}", addressId, id);

        await _contactService.RemoveAddressAsync(id, addressId, ct);

        return NoContent();
    }

    /// <summary>
    /// Sets an address as the primary address
    /// </summary>
    [HttpPut("{id}/addresses/{addressId}/primary")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetPrimaryAddress(Guid id, Guid addressId, CancellationToken ct)
    {
        _logger.LogInformation("Setting address {AddressId} as primary for contact {ContactId}", addressId, id);

        await _contactService.SetPrimaryAddressAsync(id, addressId, ct);

        return NoContent();
    }

    /// <summary>
    /// Updates an existing address for a contact
    /// </summary>
    [HttpPut("{id}/addresses/{addressId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactAddressDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        Guid addressId,
        [FromBody] AddContactAddressRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating address {AddressId} for contact {ContactId}", addressId, id);

        var address = await _contactService.UpdateAddressAsync(id, addressId, request, ct);

        return ApiResponse(address);
    }

    #endregion

    #region Phone Numbers

    /// <summary>
    /// Adds a phone number to a contact
    /// </summary>
    [HttpPost("{id}/phones")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactPhoneNumberDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPhone(
        Guid id,
        [FromBody] AddPhoneRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding phone to contact {ContactId}", id);

        var phone = await _contactService.AddPhoneAsync(id, request, ct);

        return CreatedAtAction(nameof(Get), new { id }, phone);
    }

    /// <summary>
    /// Updates a phone number
    /// </summary>
    [HttpPut("phones/{phoneId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactPhoneNumberDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdatePhone(
        Guid phoneId,
        [FromBody] AddPhoneRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating phone {PhoneId}", phoneId);

        var phone = await _contactService.UpdatePhoneAsync(phoneId, request, ct);

        return ApiResponse(phone);
    }

    /// <summary>
    /// Removes a phone number
    /// </summary>
    [HttpDelete("phones/{phoneId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemovePhone(Guid phoneId, CancellationToken ct)
    {
        _logger.LogInformation("Removing phone {PhoneId}", phoneId);

        await _contactService.RemovePhoneAsync(phoneId, ct);

        return NoContent();
    }

    /// <summary>
    /// Sets a phone as the primary phone
    /// </summary>
    [HttpPut("{id}/phones/{phoneId}/primary")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetPrimaryPhone(Guid id, Guid phoneId, CancellationToken ct)
    {
        _logger.LogInformation("Setting phone {PhoneId} as primary for contact {ContactId}", phoneId, id);

        await _contactService.SetPrimaryPhoneAsync(id, phoneId, ct);

        return NoContent();
    }

    #endregion

    #region Email Addresses

    /// <summary>
    /// Adds an email address to a contact
    /// </summary>
    [HttpPost("{id}/emails")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactEmailAddressDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddEmail(
        Guid id,
        [FromBody] AddEmailRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding email to contact {ContactId}", id);

        var email = await _contactService.AddEmailAsync(id, request, ct);

        return CreatedAtAction(nameof(Get), new { id }, email);
    }

    /// <summary>
    /// Updates an email address
    /// </summary>
    [HttpPut("emails/{emailId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactEmailAddressDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateEmail(
        Guid emailId,
        [FromBody] AddEmailRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating email {EmailId}", emailId);

        var email = await _contactService.UpdateEmailAsync(emailId, request, ct);

        return ApiResponse(email);
    }

    /// <summary>
    /// Removes an email address
    /// </summary>
    [HttpDelete("emails/{emailId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveEmail(Guid emailId, CancellationToken ct)
    {
        _logger.LogInformation("Removing email {EmailId}", emailId);

        await _contactService.RemoveEmailAsync(emailId, ct);

        return NoContent();
    }

    /// <summary>
    /// Sets an email as the primary email
    /// </summary>
    [HttpPut("{id}/emails/{emailId}/primary")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetPrimaryEmail(Guid id, Guid emailId, CancellationToken ct)
    {
        _logger.LogInformation("Setting email {EmailId} as primary for contact {ContactId}", emailId, id);

        await _contactService.SetPrimaryEmailAsync(id, emailId, ct);

        return NoContent();
    }

    #endregion

    #region Profile Image

    /// <summary>
    /// Uploads a profile image for a contact
    /// </summary>
    [HttpPost("{id}/profile-image")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UploadProfileImage(
        Guid id,
        IFormFile file,
        CancellationToken ct)
    {
        _logger.LogInformation("Uploading profile image for contact {ContactId}", id);

        if (file.Length == 0)
        {
            return ErrorResponse("No file provided");
        }

        if (file.Length > 5 * 1024 * 1024) // 5MB limit
        {
            return ErrorResponse("File size exceeds 5MB limit");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return ErrorResponse("Invalid file type. Allowed types: JPG, PNG, GIF, WebP");
        }

        using var stream = file.OpenReadStream();
        var imageUrl = await _contactService.UploadProfileImageAsync(id, stream, file.FileName, ct);

        return ApiResponse(new { imageUrl });
    }

    /// <summary>
    /// Deletes the profile image for a contact
    /// </summary>
    [HttpDelete("{id}/profile-image")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteProfileImage(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting profile image for contact {ContactId}", id);

        await _contactService.DeleteProfileImageAsync(id, ct);

        return NoContent();
    }

    /// <summary>
    /// Gets the profile image for a contact.
    /// Accepts either Authorization header OR a valid access token in query string.
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="token">Optional access token for browser-initiated requests</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{id}/profile-image")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProfileImage(Guid id, [FromQuery] string? token, CancellationToken ct)
    {
        _logger.LogInformation("Getting profile image for contact {ContactId}", id);

        // Check authorization: either authenticated user OR valid token
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var hasValidToken = !string.IsNullOrEmpty(token) &&
            _tokenService.ValidateToken(token, "contact-profile-image", id, TenantId);

        if (!isAuthenticated && !hasValidToken)
        {
            return Unauthorized();
        }

        var contact = await _contactService.GetByIdAsync(id, ct);
        if (contact == null)
        {
            return NotFoundResponse("Contact not found");
        }

        if (string.IsNullOrEmpty(contact.ProfileImageFileName))
        {
            return NotFoundResponse("Contact does not have a profile image");
        }

        var stream = await _fileStorageService.GetContactProfileImageStreamAsync(id, contact.ProfileImageFileName, ct);
        if (stream == null)
        {
            _logger.LogWarning("Profile image file not found: contact {ContactId}, file {FileName}", id, contact.ProfileImageFileName);
            return NotFoundResponse("Profile image file not found");
        }

        // Determine content type from file extension
        var contentType = Path.GetExtension(contact.ProfileImageFileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return File(stream, contentType);
    }

    #endregion

    #region Social Media

    /// <summary>
    /// Adds a social media profile to a contact
    /// </summary>
    [HttpPost("{id}/social-media")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactSocialMediaDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> AddSocialMedia(
        Guid id,
        [FromBody] AddSocialMediaRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding social media {Service} to contact {ContactId}", request.Service, id);

        var social = await _contactService.AddSocialMediaAsync(id, request, ct);

        return CreatedAtAction(nameof(Get), new { id }, social);
    }

    /// <summary>
    /// Updates a social media profile
    /// </summary>
    [HttpPut("social-media/{socialMediaId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactSocialMediaDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateSocialMedia(
        Guid socialMediaId,
        [FromBody] AddSocialMediaRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating social media {SocialMediaId}", socialMediaId);

        var social = await _contactService.UpdateSocialMediaAsync(socialMediaId, request, ct);

        return ApiResponse(social);
    }

    /// <summary>
    /// Removes a social media profile
    /// </summary>
    [HttpDelete("social-media/{socialMediaId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveSocialMedia(Guid socialMediaId, CancellationToken ct)
    {
        _logger.LogInformation("Removing social media {SocialMediaId}", socialMediaId);

        await _contactService.RemoveSocialMediaAsync(socialMediaId, ct);

        return NoContent();
    }

    #endregion

    #region Relationships

    /// <summary>
    /// Gets all relationships for a contact
    /// </summary>
    [HttpGet("{id}/relationships")]
    [ProducesResponseType(typeof(List<ContactRelationshipDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRelationships(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting relationships for contact {ContactId}", id);

        var relationships = await _contactService.GetRelationshipsAsync(id, ct);

        return ApiResponse(relationships);
    }

    /// <summary>
    /// Adds a relationship to a contact
    /// </summary>
    [HttpPost("{id}/relationships")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactRelationshipDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> AddRelationship(
        Guid id,
        [FromBody] AddRelationshipRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding relationship to contact {ContactId}", id);

        var relationship = await _contactService.AddRelationshipAsync(id, request, ct);

        return CreatedAtAction(nameof(GetRelationships), new { id }, relationship);
    }

    /// <summary>
    /// Removes a relationship
    /// </summary>
    [HttpDelete("relationships/{relationshipId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveRelationship(
        Guid relationshipId,
        [FromQuery] bool removeInverse = true,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Removing relationship {RelationshipId}", relationshipId);

        await _contactService.RemoveRelationshipAsync(relationshipId, removeInverse, ct);

        return NoContent();
    }

    #endregion

    #region Tags

    /// <summary>
    /// Gets all contact tags
    /// </summary>
    [HttpGet("tags")]
    [ProducesResponseType(typeof(List<ContactTagDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ListTags(CancellationToken ct)
    {
        _logger.LogInformation("Listing contact tags for tenant {TenantId}", TenantId);

        var tags = await _contactService.ListTagsAsync(ct);

        return ApiResponse(tags);
    }

    /// <summary>
    /// Creates a new contact tag
    /// </summary>
    [HttpPost("tags")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactTagDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateTag(
        [FromBody] CreateContactTagRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating contact tag '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var tag = await _contactService.CreateTagAsync(request, ct);

        return CreatedAtAction(nameof(ListTags), tag);
    }

    /// <summary>
    /// Updates a contact tag
    /// </summary>
    [HttpPut("tags/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactTagDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateTag(
        Guid id,
        [FromBody] UpdateContactTagRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating contact tag {TagId} for tenant {TenantId}", id, TenantId);

        var tag = await _contactService.UpdateTagAsync(id, request, ct);

        return ApiResponse(tag);
    }

    /// <summary>
    /// Deletes a contact tag
    /// </summary>
    [HttpDelete("tags/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTag(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting contact tag {TagId} for tenant {TenantId}", id, TenantId);

        await _contactService.DeleteTagAsync(id, ct);

        return NoContent();
    }

    /// <summary>
    /// Adds a tag to a contact
    /// </summary>
    [HttpPost("{id}/tags/{tagId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddTagToContact(Guid id, Guid tagId, CancellationToken ct)
    {
        _logger.LogInformation("Adding tag {TagId} to contact {ContactId}", tagId, id);

        await _contactService.AddTagToContactAsync(id, tagId, ct);

        return NoContent();
    }

    /// <summary>
    /// Removes a tag from a contact
    /// </summary>
    [HttpDelete("{id}/tags/{tagId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveTagFromContact(Guid id, Guid tagId, CancellationToken ct)
    {
        _logger.LogInformation("Removing tag {TagId} from contact {ContactId}", tagId, id);

        await _contactService.RemoveTagFromContactAsync(id, tagId, ct);

        return NoContent();
    }

    #endregion

    #region Sharing

    /// <summary>
    /// Gets sharing info for a contact
    /// </summary>
    [HttpGet("{id}/shares")]
    [ProducesResponseType(typeof(List<ContactUserShareDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetShares(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting shares for contact {ContactId}", id);

        var shares = await _contactService.GetSharesAsync(id, ct);

        return ApiResponse(shares);
    }

    /// <summary>
    /// Shares a contact with another user
    /// </summary>
    [HttpPost("{id}/shares")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactUserShareDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> ShareContact(
        Guid id,
        [FromBody] ShareContactRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Sharing contact {ContactId} with user {UserId}", id, request.SharedWithUserId);

        var share = await _contactService.ShareContactAsync(id, request, ct);

        return CreatedAtAction(nameof(GetShares), new { id }, share);
    }

    /// <summary>
    /// Updates share permissions
    /// </summary>
    [HttpPut("shares/{shareId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ContactUserShareDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateShare(
        Guid shareId,
        [FromBody] bool canEdit,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating share {ShareId}", shareId);

        var share = await _contactService.UpdateShareAsync(shareId, canEdit, ct);

        return ApiResponse(share);
    }

    /// <summary>
    /// Removes a share
    /// </summary>
    [HttpDelete("shares/{shareId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveShare(Guid shareId, CancellationToken ct)
    {
        _logger.LogInformation("Removing share {ShareId}", shareId);

        await _contactService.RemoveShareAsync(shareId, ct);

        return NoContent();
    }

    #endregion

    #region Audit Log

    /// <summary>
    /// Gets audit log for a contact
    /// </summary>
    [HttpGet("{id}/audit-log")]
    [ProducesResponseType(typeof(List<ContactAuditLogDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAuditLog(
        Guid id,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        _logger.LogInformation("Getting audit log for contact {ContactId}", id);

        var logs = await _contactService.GetAuditLogAsync(id, limit, ct);

        return ApiResponse(logs);
    }

    #endregion
}
