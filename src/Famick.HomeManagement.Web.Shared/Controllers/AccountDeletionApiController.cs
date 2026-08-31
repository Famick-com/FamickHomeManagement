using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.Account;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Account and household deletion.
/// </summary>
/// <remarks>
/// App Store Review Guideline 5.1.1(v) requires that an app offering account creation
/// also offers deletion from inside the app, so these have to be reachable by the mobile
/// client and not only from a web settings page.
/// </remarks>
/// <remarks>
/// Routed under <c>api/v1</c> rather than <c>api/auth</c> even though it is account
/// management. Everything under <c>api/auth/</c> is anonymous by default to the mobile
/// client — that prefix is where sign-in lives, so its handler withholds the bearer token
/// unless a path is explicitly named. Four endpoints already have to opt back in, and one
/// carries a comment about the 401 that followed from forgetting. There is no such
/// ambiguity under <c>api/v1</c>.
/// </remarks>
[ApiController]
[Route("api/v1/account/deletion")]
[Authorize]
public class AccountDeletionApiController : ControllerBase
{
    private readonly IAccountDeletionService _deletionService;
    private readonly ILogger<AccountDeletionApiController> _logger;

    public AccountDeletionApiController(
        IAccountDeletionService deletionService,
        ILogger<AccountDeletionApiController> logger)
    {
        _deletionService = deletionService;
        _logger = logger;
    }

    /// <summary>
    /// What deleting would destroy, and whether a deletion is already scheduled.
    /// </summary>
    /// <remarks>
    /// Clients should call this before showing the confirmation prompt: it reports the
    /// household name and how many other people are in it, so the warning can name what
    /// is actually at stake instead of saying "your data".
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(AccountDeletionStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        return Ok(await _deletionService.GetStatusAsync(userId, ct));
    }

    /// <summary>
    /// Schedules deletion. An admin's request takes the whole household; a member's takes
    /// only their own account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDeletionRequestResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Request(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await _deletionService.RequestAsync(userId, ct);

        _logger.LogInformation(
            "Deletion requested by {UserId}: scope {Scope}, purge after {PurgeAfter}",
            userId, result.Scope, result.PurgeAfter);

        return Ok(result);
    }

    /// <summary>
    /// Calls off a pending deletion.
    /// </summary>
    /// <remarks>
    /// Signing in already does this, so the endpoint exists for the explicit control — a
    /// person who opens the app during the grace period should be able to say "keep my
    /// account" and see it confirmed, rather than having to trust that logging in was
    /// enough.
    /// </remarks>
    [HttpDelete]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var cancelled = await _deletionService.CancelAsync(userId, ct);

        return Ok(new { cancelled });
    }

    /// <summary>
    /// Marks the "your deletion was cancelled" notice as shown.
    /// </summary>
    /// <remarks>
    /// Separate from reading the status so a background refresh cannot consume the notice
    /// before anyone has seen it. The client calls this after it has actually told the user.
    /// </remarks>
    [HttpPost("notice/acknowledge")]
    public async Task<IActionResult> AcknowledgeNotice(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        await _deletionService.AcknowledgeCancelledNoticeAsync(userId, ct);

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(raw, out userId);
    }
}
