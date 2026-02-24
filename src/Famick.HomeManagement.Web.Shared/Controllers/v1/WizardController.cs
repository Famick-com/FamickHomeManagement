using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for the onboarding wizard
/// </summary>
[ApiController]
[Route("api/v1/wizard")]
[Authorize]
public class WizardController : ApiControllerBase
{
    private readonly IWizardService _wizardService;

    public WizardController(
        IWizardService wizardService,
        ITenantProvider tenantProvider,
        ILogger<WizardController> logger)
        : base(tenantProvider, logger)
    {
        _wizardService = wizardService;
    }

    /// <summary>
    /// Gets the complete wizard state for all 5 pages
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(WizardStateDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetWizardState(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting wizard state for tenant {TenantId}", TenantId);

        var state = await _wizardService.GetWizardStateAsync(cancellationToken);

        return ApiResponse(state);
    }

    /// <summary>
    /// Saves household info (page 1)
    /// </summary>
    [HttpPut("household-info")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SaveHouseholdInfo(
        [FromBody] HouseholdInfoDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving household info for tenant {TenantId}", TenantId);

        await _wizardService.SaveHouseholdInfoAsync(request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Saves home statistics (page 3)
    /// </summary>
    [HttpPut("home-statistics")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SaveHomeStatistics(
        [FromBody] HomeStatisticsDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving home statistics for tenant {TenantId}", TenantId);

        await _wizardService.SaveHomeStatisticsAsync(request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Saves maintenance items (page 4)
    /// </summary>
    [HttpPut("maintenance-items")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SaveMaintenanceItems(
        [FromBody] MaintenanceItemsDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving maintenance items for tenant {TenantId}", TenantId);

        await _wizardService.SaveMaintenanceItemsAsync(request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Gets household members
    /// </summary>
    [HttpGet("members")]
    [ProducesResponseType(typeof(List<HouseholdMemberDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetHouseholdMembers(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting household members for tenant {TenantId}", TenantId);

        var members = await _wizardService.GetHouseholdMembersAsync(cancellationToken);

        return ApiResponse(members);
    }

    /// <summary>
    /// Creates or updates the current user's contact record
    /// </summary>
    [HttpPut("members/me")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HouseholdMemberDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SaveCurrentUserContact(
        [FromBody] SaveCurrentUserContactRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving current user contact for tenant {TenantId}", TenantId);

        var member = await _wizardService.SaveCurrentUserContactAsync(request, cancellationToken);

        return ApiResponse(member);
    }

    /// <summary>
    /// Adds a household member
    /// </summary>
    [HttpPost("members")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HouseholdMemberDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> AddHouseholdMember(
        [FromBody] AddHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding household member {FirstName} {LastName} for tenant {TenantId}",
            request.FirstName, request.LastName, TenantId);

        var member = await _wizardService.AddHouseholdMemberAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetHouseholdMembers), member);
    }

    /// <summary>
    /// Updates a household member's relationship
    /// </summary>
    [HttpPut("members/{contactId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HouseholdMemberDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateHouseholdMember(
        Guid contactId,
        [FromBody] UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating household member {ContactId} for tenant {TenantId}", contactId, TenantId);

        var member = await _wizardService.UpdateHouseholdMemberAsync(contactId, request, cancellationToken);

        return ApiResponse(member);
    }

    /// <summary>
    /// Removes a member from the household (unlinks, does not delete)
    /// </summary>
    [HttpDelete("members/{contactId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveHouseholdMember(
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing household member {ContactId} for tenant {TenantId}", contactId, TenantId);

        await _wizardService.RemoveHouseholdMemberAsync(contactId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Checks for duplicate contacts by name
    /// </summary>
    [HttpPost("members/check-duplicate")]
    [ProducesResponseType(typeof(DuplicateContactResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CheckDuplicateContact(
        [FromBody] CheckDuplicateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for duplicate contacts: {FirstName} {LastName}", request.FirstName, request.LastName);

        var result = await _wizardService.CheckDuplicateContactAsync(request, cancellationToken);

        return ApiResponse(result);
    }

    /// <summary>
    /// Marks the wizard as complete
    /// </summary>
    [HttpPost("complete")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CompleteWizard(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing wizard for tenant {TenantId}", TenantId);

        await _wizardService.CompleteWizardAsync(cancellationToken);

        return NoContent();
    }
}
