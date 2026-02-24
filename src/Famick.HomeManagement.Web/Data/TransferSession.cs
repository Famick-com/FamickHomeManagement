using Famick.HomeManagement.Core.DTOs.Transfer;

namespace Famick.HomeManagement.Web.Data;

/// <summary>
/// Tracks a data transfer session from self-hosted to cloud.
/// Persisted locally for resumability.
/// </summary>
public class TransferSession
{
    public Guid Id { get; set; }
    public string CloudUrl { get; set; } = "https://app.famick.com";
    public string CloudEmail { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public bool IncludeHistory { get; set; }
    public TransferSessionStatus Status { get; set; }
    public string? CurrentCategory { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<TransferItemLog> Items { get; set; } = new List<TransferItemLog>();
}
