using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.AuthProxy;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Admin-only endpoints driving the Settings → "Cloud Auth Pairing" UI
/// section. Wraps <see cref="IAuthProxyPairingService"/> so the Razor
/// component can talk to a same-origin URL via <c>IApiClient</c>.
/// </summary>
[Route("api/auth-proxy/pairing")]
[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AuthProxyPairingApiController : ControllerBase
{
    private readonly IAuthProxyPairingService _pairingService;

    public AuthProxyPairingApiController(IAuthProxyPairingService pairingService)
    {
        _pairingService = pairingService;
    }

    /// <summary>
    /// Returns 200 with <c>IsPaired</c> set either way — the UI branches
    /// on the flag. Avoids 204-with-empty-body which breaks JSON parsers
    /// on the client side.
    ///
    /// When paired, also fetches the subscription/trial state from
    /// AuthProxy (5-min cached) so the UI can render trial countdown +
    /// sign-up CTA. If the upstream call fails the subscription fields
    /// stay null and the UI shows a "status unavailable" line rather
    /// than blocking the pairing display.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(PairingStatusResponse), 200)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var current = await _pairingService.GetCurrentAsync(ct);
        if (current is null)
        {
            return Ok(new PairingStatusResponse { IsPaired = false });
        }

        var response = new PairingStatusResponse
        {
            IsPaired = true,
            AuthProxyHomeServerId = current.AuthProxyHomeServerId,
            AuthProxyBaseUrl = current.AuthProxyBaseUrl,
            DisplayName = current.DisplayName,
            PairedAdminEmail = current.PairedAdminEmail,
            PairedAt = current.PairedAt,
        };

        var billing = await _pairingService.GetBillingStatusAsync(current.AuthProxyHomeServerId, ct);
        if (billing is not null)
        {
            response.SubscriptionStatus = billing.Status;
            response.TrialEndsAt = billing.TrialEndsAt;
            response.CurrentPeriodEndsAt = billing.CurrentPeriodEndsAt;
            response.BillingUrl = billing.BillingUrl;
            response.LastStatusFetchAt = DateTime.UtcNow;
        }

        return Ok(response);
    }

    /// <summary>
    /// Completes pairing: forwards the token + display name to AuthProxy
    /// along with this home server's URL + public-key PEM + fingerprint.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(PairingStatusResponse), 200)]
    [ProducesResponseType(typeof(PairingErrorResponse), 400)]
    [ProducesResponseType(typeof(PairingErrorResponse), 409)]
    public async Task<IActionResult> Complete([FromBody] CompletePairingRequest request, CancellationToken ct)
    {
        // Pull the caller's email from the JWT — used as audit (not
        // sent to AuthProxy; AuthProxy uses the email captured at /start).
        var callerEmail = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? "(unknown)";

        // Build a request-relative public URL fallback in case the admin
        // didn't supply one. Uses the Host header — adequate behind a
        // single reverse-proxy hop; admins on more complex setups should
        // type the canonical URL explicitly.
        var hostUrl = $"{Request.Scheme}://{Request.Host}";

        var result = await _pairingService.CompletePairingAsync(request, callerEmail, hostUrl, ct);

        if (result.IsSuccess)
        {
            var config = result.Config!;
            return Ok(new PairingStatusResponse
            {
                IsPaired = true,
                AuthProxyHomeServerId = config.AuthProxyHomeServerId,
                AuthProxyBaseUrl = config.AuthProxyBaseUrl,
                DisplayName = config.DisplayName,
                PairedAdminEmail = config.PairedAdminEmail,
                PairedAt = config.PairedAt,
            });
        }

        // URL collision is the one outcome that's truly 409; everything
        // else is "you gave us bad data" → 400.
        var status = result.ErrorCode == AuthProxyPairingErrorCodes.UrlAlreadyPaired ? 409 : 400;
        return StatusCode(status, new PairingErrorResponse
        {
            ErrorCode = result.ErrorCode ?? AuthProxyPairingErrorCodes.MalformedInput,
            Error = result.ErrorMessage ?? "Pairing failed.",
        });
    }

    /// <summary>
    /// Drops the local pairing config. AuthProxy-side row is NOT removed
    /// (MVP — admin can clean up the orphan in AuthProxy's admin if they
    /// care).
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Unpair(CancellationToken ct)
    {
        await _pairingService.UnpairAsync(ct);
        return NoContent();
    }
}
