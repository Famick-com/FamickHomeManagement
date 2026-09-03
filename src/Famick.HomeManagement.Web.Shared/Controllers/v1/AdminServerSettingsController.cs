using Famick.HomeManagement.Core.DTOs.Server;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Admin-only read/write of the self-hosted server-config.json overlay.
/// Backs the Settings → Server Settings page; operators can also edit the
/// file directly on the host (reloadOnChange picks up either path).
/// </summary>
[ApiController]
[Route("api/v1/admin/server-settings")]
[Authorize(Policy = "RequireAdmin")]
[SingleTenantOnly]
public class AdminServerSettingsController : ApiControllerBase
{
    private readonly IServerConfigService _serverConfigService;

    public AdminServerSettingsController(
        IServerConfigService serverConfigService,
        ITenantProvider tenantProvider,
        ILogger<AdminServerSettingsController> logger)
        : base(tenantProvider, logger)
    {
        _serverConfigService = serverConfigService;
    }

    /// <summary>
    /// Returns the current contents of server-config.json. Missing file =
    /// returns the DTO with default values so the form can render.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ServerConfigDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var config = await _serverConfigService.GetAsync(cancellationToken);
        return ApiResponse(config);
    }

    /// <summary>
    /// Atomically rewrites server-config.json with the supplied values. Full
    /// replace — the caller is expected to have round-tripped through GET first
    /// to preserve sections it doesn't intend to edit.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Update(
        [FromBody] ServerConfigDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin updating server-config.json");
        await _serverConfigService.UpdateAsync(request, cancellationToken);
        return NoContent();
    }
}
