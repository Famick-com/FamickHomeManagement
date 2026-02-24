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

    public ProfileController(
        IUserProfileService profileService,
        ITenantProvider tenantProvider,
        ILogger<ProfileController> logger)
        : base(tenantProvider, logger)
    {
        _profileService = profileService;
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
}
