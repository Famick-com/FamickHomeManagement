using Famick.HomeManagement.Core.DTOs.Transfer;

namespace Famick.HomeManagement.Web.Services;

/// <summary>
/// Orchestrates data transfer from self-hosted instance to Famick cloud.
/// Reads local data, compares with cloud, and creates missing items via cloud CRUD endpoints.
/// </summary>
public interface ICloudTransferService
{
    /// <summary>
    /// Authenticate against the cloud API and create/update a transfer session.
    /// </summary>
    Task<TransferAuthenticateResponse> AuthenticateAsync(TransferAuthenticateRequest request, CancellationToken ct);

    /// <summary>
    /// Get a summary of local data counts per category.
    /// </summary>
    Task<TransferDataSummary> GetSummaryAsync(CancellationToken ct);

    /// <summary>
    /// Check if there's an incomplete transfer session that can be resumed.
    /// </summary>
    Task<TransferSessionInfo> GetSessionInfoAsync(CancellationToken ct);

    /// <summary>
    /// Start or resume a transfer. Runs as a background operation.
    /// Returns immediately with the session ID.
    /// </summary>
    Task<TransferStartResponse> StartTransferAsync(TransferStartRequest request, CancellationToken ct);

    /// <summary>
    /// Get the current transfer progress (polled by UI).
    /// </summary>
    TransferProgress? GetCurrentProgress();

    /// <summary>
    /// Cancel a running transfer.
    /// </summary>
    void CancelTransfer();

    /// <summary>
    /// Get final results after transfer completes.
    /// </summary>
    Task<List<TransferCategoryResult>> GetResultsAsync(CancellationToken ct);
}
