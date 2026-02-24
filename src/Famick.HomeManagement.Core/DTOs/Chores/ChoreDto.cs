namespace Famick.HomeManagement.Core.DTOs.Chores;

public class ChoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "manually";
    public int? PeriodDays { get; set; }
    public bool TrackDateOnly { get; set; }
    public bool Rollover { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentConfig { get; set; }
    public Guid? NextExecutionAssignedToUserId { get; set; }
    public string? NextExecutionAssignedToUserName { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public bool ConsumeProductOnExecution { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? ProductAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
