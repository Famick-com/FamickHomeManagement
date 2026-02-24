namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Information about an existing transfer session, returned when checking for incomplete sessions.
/// </summary>
public class TransferSessionInfo
{
    public bool HasIncompleteSession { get; set; }
    public Guid? SessionId { get; set; }
    public string? CurrentCategory { get; set; }
    public DateTime? StartedAt { get; set; }
}
