using Famick.HomeManagement.Core.DTOs.Transfer;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Services;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Controllers;

/// <summary>
/// API controller for transferring data from self-hosted to Famick cloud.
/// Admin-only. Called by the Blazor WASM UI.
/// </summary>
[ApiController]
[Route("api/v1/transfer")]
[Authorize(Policy = "RequireAdmin")]
public class TransferController : ApiControllerBase
{
    private readonly ICloudTransferService _transferService;
    private readonly IFeatureManager _featureManager;

    public TransferController(
        ICloudTransferService transferService,
        IFeatureManager featureManager,
        ITenantProvider tenantProvider,
        ILogger<TransferController> logger)
        : base(tenantProvider, logger)
    {
        _transferService = transferService;
        _featureManager = featureManager;
    }

    /// <summary>
    /// Simple availability check. Returns 200 on self-hosted to indicate
    /// the transfer feature exists. No DB access, no feature flag check.
    /// Used by the UI to decide whether to show the Transfer to Cloud section.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(200)]
    public IActionResult GetAvailable() => Ok();

    /// <summary>
    /// Authenticate against the cloud API and create a transfer session.
    /// </summary>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(TransferAuthenticateResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Authenticate(
        [FromBody] TransferAuthenticateRequest request,
        CancellationToken ct)
    {
        if (!_featureManager.IsEnabled(FeatureNames.TransferToCloud))
            return ErrorResponse("Transfer to Cloud is not enabled", 403);

        var result = await _transferService.AuthenticateAsync(request, ct);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get a summary of local data counts per category.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(TransferDataSummary), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        if (!_featureManager.IsEnabled(FeatureNames.TransferToCloud))
            return ErrorResponse("Transfer to Cloud is not enabled", 403);

        var summary = await _transferService.GetSummaryAsync(ct);
        return ApiResponse(summary);
    }

    /// <summary>
    /// Check for an incomplete transfer session that can be resumed.
    /// </summary>
    [HttpGet("session")]
    [ProducesResponseType(typeof(TransferSessionInfo), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSession(CancellationToken ct)
    {
        var info = await _transferService.GetSessionInfoAsync(ct);
        return ApiResponse(info);
    }

    /// <summary>
    /// Start or resume a data transfer. Kicks off a background task and returns immediately.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(TransferStartResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Start(
        [FromBody] TransferStartRequest request,
        CancellationToken ct)
    {
        if (!_featureManager.IsEnabled(FeatureNames.TransferToCloud))
            return ErrorResponse("Transfer to Cloud is not enabled", 403);

        try
        {
            var result = await _transferService.StartTransferAsync(request, ct);
            return ApiResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Poll for current transfer progress. Called every 1-2 seconds by the UI.
    /// </summary>
    [HttpGet("progress")]
    [ProducesResponseType(typeof(TransferProgress), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public IActionResult GetProgress()
    {
        var progress = _transferService.GetCurrentProgress();
        if (progress == null)
            return EmptyApiResponse();

        return ApiResponse(progress);
    }

    /// <summary>
    /// Cancel a running transfer.
    /// </summary>
    [HttpPost("cancel")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public IActionResult Cancel()
    {
        _transferService.CancelTransfer();
        return EmptyApiResponse();
    }

    /// <summary>
    /// Get final results after transfer completes.
    /// </summary>
    [HttpGet("results")]
    [ProducesResponseType(typeof(List<TransferCategoryResult>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetResults(CancellationToken ct)
    {
        var results = await _transferService.GetResultsAsync(ct);
        return ApiResponse(results);
    }
}
