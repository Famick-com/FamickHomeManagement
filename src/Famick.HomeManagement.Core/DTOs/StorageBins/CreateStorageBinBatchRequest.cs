namespace Famick.HomeManagement.Core.DTOs.StorageBins;

/// <summary>
/// Request to create multiple storage bins at once (for label printing)
/// </summary>
public class CreateStorageBinBatchRequest
{
    /// <summary>
    /// Number of storage bins to create
    /// </summary>
    public int Count { get; set; } = 1;
}
