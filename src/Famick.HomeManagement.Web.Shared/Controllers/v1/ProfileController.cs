using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for user self-service profile operations
/// </summary>
[ApiController]
[Route("api/v1/profile")]
[Authorize]
public class ProfileController : ApiControllerBase
{
    private readonly IUserProfileService _profileService;
    private readonly IContactService _contactService;
    private readonly IFileStorageService _fileStorageService;

    public ProfileController(
        IUserProfileService profileService,
        IContactService contactService,
        IFileStorageService fileStorageService,
        ITenantProvider tenantProvider,
        ILogger<ProfileController> logger)
        : base(tenantProvider, logger)
    {
        _profileService = profileService;
        _contactService = contactService;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// Gets the current user ID from JWT claims
    /// </summary>
    private Guid CurrentUserId
    {
        get
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new InvalidOperationException("User ID not found in claims");
            }

            return userId;
        }
    }

    /// <summary>
    /// Gets the current user's profile with linked contact information
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting profile for user {UserId}", CurrentUserId);

        try
        {
            var profile = await _profileService.GetProfileAsync(CurrentUserId, cancellationToken);
            return ApiResponse(profile);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }

    /// <summary>
    /// Updates the current user's profile (name and language)
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return BadRequest(new { error_message = "First name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error_message = "Last name is required" });
        }

        _logger.LogInformation("Updating profile for user {UserId}", CurrentUserId);

        try
        {
            var profile = await _profileService.UpdateProfileAsync(CurrentUserId, request, cancellationToken);
            return ApiResponse(profile);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }

    /// <summary>
    /// Updates only the user's preferred language
    /// </summary>
    [HttpPut("language")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateLanguage(
        [FromBody] UpdateLanguageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            return BadRequest(new { error_message = "Language code is required" });
        }

        _logger.LogInformation("Updating language preference for user {UserId} to {Language}", CurrentUserId, request.LanguageCode);

        try
        {
            await _profileService.UpdatePreferredLanguageAsync(CurrentUserId, request.LanguageCode, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }

    /// <summary>
    /// Changes the current user's password
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new { error_message = "Current password is required" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error_message = "New password is required" });
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new { error_message = "Password confirmation is required" });
        }

        _logger.LogInformation("Changing password for user {UserId}", CurrentUserId);

        try
        {
            await _profileService.ChangePasswordAsync(CurrentUserId, request, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
        catch (InvalidCredentialsException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (BusinessRuleViolationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
    }

    /// <summary>
    /// Updates the user's linked contact information
    /// </summary>
    [HttpPut("contact")]
    [ProducesResponseType(typeof(ContactDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateContactInfo(
        [FromBody] UpdateContactRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating contact info for user {UserId}", CurrentUserId);

        try
        {
            var contact = await _profileService.UpdateContactInfoAsync(CurrentUserId, request, cancellationToken);
            return ApiResponse(contact);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
        catch (BusinessRuleViolationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
    }

    /// <summary>
    /// Uploads a profile image for the current user's linked contact.
    /// Any authenticated user can upload their own profile image.
    /// </summary>
    [HttpPost("profile-image")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UploadProfileImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return ErrorResponse("No file provided");

        if (file.Length > 5 * 1024 * 1024)
            return ErrorResponse("File size exceeds 5MB limit");

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
            return ErrorResponse("Only JPEG, PNG, GIF, and WebP images are allowed");

        try
        {
            var profile = await _profileService.GetProfileAsync(CurrentUserId, cancellationToken);
            if (profile.ContactId == null)
                return ErrorResponse("No linked contact found for profile image");

            using var stream = file.OpenReadStream();
            var imageUrl = await _contactService.UploadProfileImageAsync(
                profile.ContactId.Value, stream, file.FileName, cancellationToken);

            return ApiResponse(new { imageUrl });
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }

    /// <summary>
    /// Deletes the profile image for the current user's linked contact.
    /// Any authenticated user can delete their own profile image.
    /// </summary>
    [HttpDelete("profile-image")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> DeleteProfileImage(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profileService.GetProfileAsync(CurrentUserId, cancellationToken);
            if (profile.ContactId == null)
                return ErrorResponse("No linked contact found for profile image");

            await _contactService.DeleteProfileImageAsync(profile.ContactId.Value, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }

    /// <summary>
    /// Gets the profile image for the current user's linked contact.
    /// Any authenticated user can view their own profile image.
    /// </summary>
    [HttpGet("profile-image")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProfileImage(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profileService.GetProfileAsync(CurrentUserId, cancellationToken);
            if (profile.ContactId == null)
                return NotFoundResponse("No linked contact");

            var contact = await _contactService.GetByIdAsync(profile.ContactId.Value, cancellationToken);
            if (contact == null || string.IsNullOrEmpty(contact.ProfileImageFileName))
                return NotFoundResponse("No profile image");

            var stream = await _fileStorageService.GetContactProfileImageStreamAsync(
                profile.ContactId.Value, contact.ProfileImageFileName, cancellationToken);
            if (stream == null)
                return NotFoundResponse("Profile image file not found");

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
        catch (EntityNotFoundException)
        {
            return NotFoundResponse("User not found");
        }
    }
}
