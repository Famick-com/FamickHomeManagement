namespace Famick.HomeManagement.Core.DTOs.Chores;

public class UpdateChoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "manually";
    public int? PeriodDays { get; set; }
    public bool TrackDateOnly { get; set; }
    public bool Rollover { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentConfig { get; set; }
    public bool ConsumeProductOnExecution { get; set; }
    public Guid? ProductId { get; set; }
    public decimal? ProductAmount { get; set; }
}
