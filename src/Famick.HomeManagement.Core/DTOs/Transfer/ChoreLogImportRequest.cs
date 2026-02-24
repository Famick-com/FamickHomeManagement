namespace Famick.HomeManagement.Core.DTOs.Transfer;

public class ChoreLogImportRequest
{
    public Guid ChoreId { get; set; }
    public DateTime? TrackedTime { get; set; }
    public bool WasSkipped { get; set; }
}

public class ChoreLogImportResult
{
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
