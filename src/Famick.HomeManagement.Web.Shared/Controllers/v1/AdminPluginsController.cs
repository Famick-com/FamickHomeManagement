using Famick.HomeManagement.Core.DTOs.Plugins;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Admin read/write of the self-hosted plugins/config.json file. Backs the
/// Settings → Plugins page; changes require a server restart to take effect
/// (the UI surfaces that as a banner).
/// </summary>
[ApiController]
[Route("api/v1/admin/plugins")]
[Authorize(Policy = "RequireAdmin")]
public class AdminPluginsController : ApiControllerBase
{
    private readonly IPluginConfigService _service;

    public AdminPluginsController(
        IPluginConfigService service,
        ITenantProvider tenantProvider,
        ILogger<AdminPluginsController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
    }

    /// <summary>
    /// Lists built-in + configured plugins (with secrets masked) plus any
    /// DLL files in the plugins folder not yet referenced.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PluginConfigListDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var list = await _service.GetAsync(ct);
        return ApiResponse(list);
    }

    /// <summary>
    /// Creates or replaces the plugin entry for the given id. Sending
    /// <c>"***"</c> for a known secret field preserves the on-disk value.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Upsert(
        string id,
        [FromBody] PluginConfigEntryDto entry,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Admin upserting plugin {Id}", id);
        await _service.UpsertAsync(id, entry, ct);
        return NoContent();
    }

    /// <summary>
    /// Removes the plugin entry. Built-ins can be disabled but not removed.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("Admin removing plugin {Id}", id);
        try
        {
            await _service.DeleteAsync(id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        return NoContent();
    }

    /// <summary>
    /// Registers a discovered DLL as a new (disabled) external plugin entry.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPluginRequest request,
        CancellationToken ct = default)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Id)
            || string.IsNullOrWhiteSpace(request.AssemblyPath)
            || string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest(new { error = "Id, AssemblyPath, and Type are required." });
        }
        await _service.RegisterDiscoveredAsync(
            request.Id, request.AssemblyPath, request.Type, request.DisplayName ?? request.Id, ct);
        return NoContent();
    }

    public class RegisterPluginRequest
    {
        public string Id { get; set; } = string.Empty;
        public string AssemblyPath { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }
}
