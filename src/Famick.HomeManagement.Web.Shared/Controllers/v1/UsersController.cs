using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing users (Admin only)
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "RequireAdmin")]
public class UsersController : ApiControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly IHouseholdInvitationService _invitations;

    public UsersController(
        IUserManagementService userManagementService,
        IHouseholdInvitationService invitations,
        ITenantProvider tenantProvider,
        ILogger<UsersController> logger)
        : base(tenantProvider, logger)
    {
        _userManagementService = userManagementService;
        _invitations = invitations;
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
    /// Lists all users for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ManagedUserDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing users for tenant {TenantId}", TenantId);

        var users = await _userManagementService.GetAllUsersAsync(cancellationToken);
        return ApiResponse(users);
    }

    /// <summary>
    /// Gets the current user's profile (available to all authenticated users)
    /// </summary>
    [HttpGet("me")]
    [Authorize] // Override policy - any authenticated user
    [ProducesResponseType(typeof(ManagedUserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _userManagementService.GetUserByIdAsync(CurrentUserId, cancellationToken);

        if (user == null)
        {
            return NotFoundResponse("User not found");
        }

        return ApiResponse(user);
    }

    /// <summary>
    /// Gets a specific user by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ManagedUserDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting user {UserId} for tenant {TenantId}", id, TenantId);

        var user = await _userManagementService.GetUserByIdAsync(id, cancellationToken);

        if (user == null)
        {
            return NotFoundResponse($"User with ID {id} not found");
        }

        return ApiResponse(user);
    }

    /// <summary>
    /// Adds a member. A server that can send email invites them (202, with an invitation id and
    /// no user id) and creates the account when they accept; one that cannot creates it now (201).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), 201)]
    [ProducesResponseType(typeof(CreateUserResponse), 202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error_message = "Email is required" });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return BadRequest(new { error_message = "First name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error_message = "Last name is required" });
        }

        if (request.Roles.Count == 0)
        {
            return BadRequest(new { error_message = "At least one role is required" });
        }

        _logger.LogInformation("Creating user '{Email}' for tenant {TenantId}", request.Email, TenantId);

        try
        {
            var baseUrl = PublicBaseUrl.Resolve(Request);
            var response = await _userManagementService.CreateUserAsync(request, baseUrl, cancellationToken);
            return response.InvitationId != null
                ? Accepted(response)
                : CreatedAtAction(nameof(GetById), new { id = response.UserId }, response);
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { error_message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ManagedUserDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error_message = "Email is required" });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return BadRequest(new { error_message = "First name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error_message = "Last name is required" });
        }

        if (request.Roles.Count == 0)
        {
            return BadRequest(new { error_message = "At least one role is required" });
        }

        _logger.LogInformation("Updating user {UserId} for tenant {TenantId}", id, TenantId);

        try
        {
            var user = await _userManagementService.UpdateUserAsync(id, request, cancellationToken);
            return ApiResponse(user);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"User with ID {id} not found");
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { error_message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a user. Cannot delete yourself.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting user {UserId} for tenant {TenantId}", id, TenantId);

        try
        {
            await _userManagementService.DeleteUserAsync(id, CurrentUserId, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"User with ID {id} not found");
        }
        catch (BusinessRuleViolationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
    }

    /// <summary>
    /// Resets a user's password (Admin action)
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(AdminResetPasswordResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin resetting password for user {UserId}", id);

        try
        {
            var response = await _userManagementService.AdminResetPasswordAsync(id, request, cancellationToken);
            return ApiResponse(response);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"User with ID {id} not found");
        }
    }

    /// <summary>
    /// Links a contact to a user
    /// </summary>
    [HttpPut("{id}/contact/{contactId}")]
    [ProducesResponseType(typeof(ManagedUserDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> LinkContact(
        Guid id,
        Guid contactId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Linking contact {ContactId} to user {UserId}", contactId, id);

        try
        {
            var user = await _userManagementService.LinkContactAsync(id, contactId, cancellationToken);
            return ApiResponse(user);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Unlinks the contact from a user
    /// </summary>
    [HttpDelete("{id}/contact")]
    [ProducesResponseType(typeof(ManagedUserDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnlinkContact(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unlinking contact from user {UserId}", id);

        try
        {
            var user = await _userManagementService.UnlinkContactAsync(id, cancellationToken);
            return ApiResponse(user);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"User with ID {id} not found");
        }
    }

    /// <summary>
    /// The household's pending invitations, and whether this server sends invitations at all
    /// </summary>
    [HttpGet("invitations")]
    [ProducesResponseType(typeof(HouseholdInvitationListResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ListInvitations(CancellationToken cancellationToken)
    {
        var invitations = _invitations.InvitationsEnabled
            ? await _invitations.GetPendingAsync(cancellationToken)
            : new List<HouseholdInvitationDto>();

        return ApiResponse(new HouseholdInvitationListResponse
        {
            InvitationsEnabled = _invitations.InvitationsEnabled,
            Invitations = invitations
        });
    }

    /// <summary>
    /// Sends a pending invitation again, with a new link
    /// </summary>
    [HttpPost("invitations/{id:guid}/resend")]
    [ProducesResponseType(typeof(HouseholdInvitationDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResendInvitation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var invitation = await _invitations.ResendAsync(id, PublicBaseUrl.Resolve(Request), cancellationToken);
            return ApiResponse(invitation);
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"Invitation with ID {id} not found");
        }
    }

    /// <summary>
    /// Withdraws a pending invitation
    /// </summary>
    [HttpDelete("invitations/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _invitations.RevokeAsync(id, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFoundResponse($"Invitation with ID {id} not found");
        }
    }
}
