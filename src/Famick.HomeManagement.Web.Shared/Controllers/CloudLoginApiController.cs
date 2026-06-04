using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.CloudLogin;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Per-user opt-in toggle for the cloud-login flow. Authenticated user
/// only — admin can call <c>OptInOtherAsync</c> separately for bulk
/// enrolment (future scope).
/// </summary>
[Route("api/profile/cloud-login")]
[ApiController]
[Authorize]
public class CloudLoginApiController : ControllerBase
{
    private readonly ICloudLoginOptInService _service;
    private readonly IAuthProxyPairingService _pairingService;

    public CloudLoginApiController(
        ICloudLoginOptInService service,
        IAuthProxyPairingService pairingService)
    {
        _service = service;
        _pairingService = pairingService;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(CloudLoginStatusResponse), 200)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var pairing = await _pairingService.GetCurrentAsync(ct);
        var userId = GetUserId();
        var optedIn = userId is not null && await _service.IsOptedInAsync(userId.Value, ct);
        return Ok(new CloudLoginStatusResponse
        {
            ServerIsPaired = pairing is not null,
            UserIsOptedIn = optedIn,
        });
    }

    [HttpPost("opt-in")]
    [ProducesResponseType(typeof(CloudLoginStatusResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> OptIn(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var pairing = await _pairingService.GetCurrentAsync(ct);
        if (pairing is null)
        {
            return Conflict(new { error = "Home server is not paired with auth.famick.com." });
        }

        await _service.OptInAsync(userId.Value, ct);
        return Ok(new CloudLoginStatusResponse { ServerIsPaired = true, UserIsOptedIn = true });
    }

    [HttpPost("opt-out")]
    [ProducesResponseType(typeof(CloudLoginStatusResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> OptOut(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _service.OptOutAsync(userId.Value, ct);
        var pairing = await _pairingService.GetCurrentAsync(ct);
        return Ok(new CloudLoginStatusResponse { ServerIsPaired = pairing is not null, UserIsOptedIn = false });
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
