namespace Famick.HomeManagement.Core.DTOs.Chores;

public class ChoreLogDto
{
    public Guid Id { get; set; }
    public Guid ChoreId { get; set; }
    public string ChoreName { get; set; } = string.Empty;
    public DateTime? TrackedTime { get; set; }
    public Guid? DoneByUserId { get; set; }
    public string? DoneByUserName { get; set; }
    public bool Undone { get; set; }
    public DateTime? UndoneTimestamp { get; set; }
    public bool Skipped { get; set; }
    public DateTime? ScheduledExecutionTime { get; set; }
    public DateTime CreatedAt { get; set; }
}
