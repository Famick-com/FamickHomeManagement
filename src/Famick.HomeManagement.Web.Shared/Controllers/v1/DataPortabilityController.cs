using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Taking a household's data out, and putting it back.
/// </summary>
/// <remarks>
/// Export is available to anyone signed in, at any tier, and is not behind a re-authentication
/// step. It is the answer to a data-access request, and putting friction in front of that is the
/// thing the feature exists to avoid. Two people cannot start competing exports of one household
/// anyway — the service hands back the run already in flight.
/// </remarks>
[Route("api/v1/data-portability")]
[Authorize]
public class DataPortabilityController(
    IHouseholdDataPortabilityService portability,
    IFileAccessTokenService tokenService,
    ITenantProvider tenantProvider,
    ILogger<DataPortabilityController> logger)
    : ApiControllerBase(tenantProvider, logger)
{
    /// <summary>
    /// What this deployment supports, and anything already in flight.
    /// </summary>
    /// <remarks>
    /// Answers rather than refuses, so a client can hide an entry point it cannot use instead of
    /// showing a button that fails.
    /// </remarks>
    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(DataPortabilityCapabilities), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapabilities(CancellationToken ct)
        => ApiResponse(await portability.GetCapabilitiesAsync(ct));

    [HttpPost("exports")]
    [ProducesResponseType(typeof(DataExportSummary), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartExport([FromBody] StartExportRequest? request, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var export = await portability.StartExportAsync(request ?? new StartExportRequest(), userId.Value, ct);

        return Accepted(export);
    }

    [HttpGet("exports/{id:guid}")]
    [ProducesResponseType(typeof(DataExportSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExport(Guid id, CancellationToken ct)
    {
        var export = await portability.GetExportAsync(id, ct);
        return export == null ? NotFoundResponse("Export not found") : ApiResponse(export);
    }

    /// <summary>
    /// The archive's manifest: what it contains, and anything that could not be included.
    /// </summary>
    [HttpGet("exports/{id:guid}/manifest")]
    [ProducesResponseType(typeof(ArchiveManifest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetManifest(Guid id, CancellationToken ct)
    {
        var manifest = await portability.GetManifestAsync(id, ct);
        return manifest == null ? NotFoundResponse("Manifest not found") : ApiResponse(manifest);
    }

    /// <summary>
    /// Downloads a finished archive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anonymous with a signed token, because the link goes out by email and has to work from a
    /// mail client. The token names the resource and the household, and
    /// <see cref="ApiControllerBase.ValidateFileAccess"/> sets the tenant context from it.
    /// </para>
    /// <para>
    /// The range header is parsed here and handed to storage rather than left to the response
    /// layer. An object-storage stream is not seekable, so a FileStreamResult cannot work out a
    /// length and quietly sends the whole archive — the wrong behaviour on the biggest file this
    /// product serves, and it breaks clients that fetch large files in pieces.
    /// </para>
    /// </remarks>
    [HttpGet("exports/{id:guid}/download")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(Guid id, [FromQuery] string? token, CancellationToken ct)
    {
        if (ValidateFileAccess(tokenService, token, "export-archive", id) == null)
            return Unauthorized();

        var (rangeStart, rangeEnd) = ParseRange();

        var download = await portability.OpenArchiveAsync(id, rangeStart, rangeEnd, ct);
        if (download == null)
        {
            // Gone rather than NotFound: the archive existed and has expired, and saying so lets
            // the client offer a new export instead of reporting a broken link.
            return StatusCode(StatusCodes.Status410Gone);
        }

        Response.Headers.AcceptRanges = "bytes";

        if (rangeStart.HasValue)
        {
            var end = rangeEnd ?? download.TotalLength - 1;
            Response.Headers.ContentRange = $"bytes {rangeStart}-{end}/{download.TotalLength}";
            Response.StatusCode = StatusCodes.Status206PartialContent;
        }

        return File(download.Content, "application/zip", download.FileName);
    }

    /// <summary>
    /// Deletes an archive before it would expire.
    /// </summary>
    [HttpDelete("exports/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExport(Guid id, CancellationToken ct)
        => await portability.DeleteExportAsync(id, ct)
            ? EmptyApiResponse()
            : NotFoundResponse("Export not found");

    /// <summary>
    /// Reads a single byte range from the request, if there is one.
    /// </summary>
    /// <remarks>
    /// Only one range is honoured. Multipart ranges would need a multipart response body, and no
    /// client of this endpoint asks for them.
    /// </remarks>
    private (long? Start, long? End) ParseRange()
    {
        var header = Request.Headers.Range.ToString();
        if (string.IsNullOrWhiteSpace(header)) return (null, null);

        if (!RangeHeaderValue.TryParse(header, out var parsed) || parsed.Ranges.Count != 1)
            return (null, null);

        var range = parsed.Ranges.Single();
        return (range.From, range.To);
    }

    private Guid? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
