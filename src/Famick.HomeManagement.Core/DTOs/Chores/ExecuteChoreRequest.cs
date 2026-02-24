namespace Famick.HomeManagement.Core.DTOs.Chores;

public class ExecuteChoreRequest
{
    public DateTime? TrackedTime { get; set; } // Defaults to now
    public Guid? DoneByUserId { get; set; } // Defaults to current user
}
