namespace Famick.HomeManagement.Core.DTOs.Chores;

public class ChoreSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public DateTime? NextExecutionDate { get; set; }
    public string? AssignedToUserName { get; set; }
    public bool IsOverdue { get; set; }
}
