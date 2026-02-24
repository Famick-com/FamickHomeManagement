namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Request to start or resume a data transfer to cloud.
/// </summary>
public class TransferStartRequest
{
    /// <summary>
    /// Whether to include history data (chore logs, maintenance records, usage logs, mileage logs)
    /// </summary>
    public bool IncludeHistory { get; set; }

    /// <summary>
    /// Whether to resume an existing incomplete transfer session
    /// </summary>
    public bool Resume { get; set; }
}

/// <summary>
/// Response after starting a transfer
/// </summary>
public class TransferStartResponse
{
    public Guid SessionId { get; set; }
}
