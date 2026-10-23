using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Famick.HomeManagement.Web.Shared.Authorization;

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
    IMemberDataExportService memberExports,
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
    [NoRolePolicy("Export is open to every member by design; see the class remarks.")]
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
    /// A signed, short-lived URL the browser can navigate to.
    /// </summary>
    /// <remarks>
    /// Needed because a plain navigation cannot carry the app's bearer token, and the archive is
    /// too large to fetch as a blob and hand to the page.
    /// </remarks>
    [HttpGet("exports/{id:guid}/download-link")]
    [ProducesResponseType(typeof(DownloadLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadLink(Guid id, CancellationToken ct)
    {
        var url = await portability.GetDownloadLinkAsync(id, ct);
        return url == null
            ? NotFoundResponse("Export not found or no longer available")
            : ApiResponse(new DownloadLinkResponse { Url = url });
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
        var (rangeStart, rangeEnd) = ParseRange();
        ExportDownloadResult result;

        // A member's own export shares this URL, and is opened by its token alone — never by a
        // session. ValidateFileAccess waves through anyone signed in to the household, which is
        // right for the household's archive and wrong for contacts only the member could see.
        if (MemberExportTenant(token, id) is { } memberTenantId)
        {
            tenantProvider.SetTenantId(memberTenantId);
            result = await memberExports.OpenArchiveAsync(id, rangeStart, rangeEnd, ct);
        }
        else
        {
            if (ValidateFileAccess(tokenService, token, "export-archive", id) == null)
                return Unauthorized();

            result = await portability.OpenArchiveAsync(id, rangeStart, rangeEnd, ct);
        }

        switch (result.Status)
        {
            case ExportDownloadStatus.Unavailable:
                // Gone rather than NotFound: the archive existed and has expired, and saying so
                // lets the client offer a new export instead of reporting a broken link.
                return StatusCode(StatusCodes.Status410Gone);

            case ExportDownloadStatus.RangeNotSatisfiable:
                Response.Headers.AcceptRanges = "bytes";
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        var download = result.Download!;
        var isPartial = rangeStart.HasValue || rangeEnd.HasValue;

        Response.Headers.AcceptRanges = "bytes";

        // Content-Length is set explicitly rather than inferred. A seekable stream would have
        // ASP.NET Core report its full length while copying only from the seek position, so a
        // range would advertise more bytes than it sends; a bounded stream is not seekable, so it
        // would advertise none at all. Both leave the client waiting.
        var start = download.RangeStart ?? 0;
        var end = download.RangeEnd ?? download.TotalLength - 1;
        Response.ContentLength = end - start + 1;

        if (isPartial)
        {
            Response.Headers.ContentRange = $"bytes {start}-{end}/{download.TotalLength}";
            Response.StatusCode = StatusCodes.Status206PartialContent;
        }

        // Deliberately the opposite of the media endpoints, which are cached hard
        // (see StoredFileCache). An archive is a one-shot download of the whole
        // household's data and it expires — the endpoint answers 410 Gone after
        // that — so a copy left in a browser cache would outlive the expiry it is
        // supposed to honour, and gains nothing in exchange.
        Response.Headers.CacheControl = "no-store";

        return File(download.Content, "application/zip", download.FileName);
    }

    /// <summary>
    /// Deletes an archive before it would expire.
    /// </summary>
    [HttpDelete("exports/{id:guid}")]
    [NoRolePolicy("Export is open to every member by design; see the class remarks.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExport(Guid id, CancellationToken ct)
        => await portability.DeleteExportAsync(id, ct)
            ? EmptyApiResponse()
            : NotFoundResponse("Export not found");

    /// <summary>
    /// Builds a copy of the caller's own data and returns a link to download it.
    /// </summary>
    /// <remarks>
    /// What leaves with the member when their account goes — contacts they kept private or shared
    /// only with named people — and their profile. Offered from the account-deletion flow so it
    /// can be taken during the grace period. Built in the request: it is one person's data, and
    /// small.
    /// </remarks>
    [HttpPost("my-data")]
    [NoRolePolicy("A member's own data is theirs to take, whatever their role.")]
    [ProducesResponseType(typeof(DownloadLinkResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMyData(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var url = await memberExports.CreateOwnExportLinkAsync(userId.Value, ct);
        return ApiResponse(new DownloadLinkResponse { Url = url });
    }

    /// <summary>
    /// The household a member-export token names, when <paramref name="token"/> is one for
    /// <paramref name="id"/>; otherwise null.
    /// </summary>
    private Guid? MemberExportTenant(string? token, Guid id) =>
        !string.IsNullOrEmpty(token)
        && tokenService.TryParseToken(token, out var claims)
        && claims!.ResourceType == MemberDataExport.TokenResourceType
        && claims.ResourceId == id
            ? claims.TenantId
            : null;

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

/// <summary>A signed URL the browser can open directly.</summary>
public sealed class DownloadLinkResponse
{
    public string Url { get; set; } = string.Empty;
}
