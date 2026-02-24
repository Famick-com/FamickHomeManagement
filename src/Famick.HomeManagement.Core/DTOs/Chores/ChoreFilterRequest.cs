namespace Famick.HomeManagement.Core.DTOs.Chores;

public class ChoreFilterRequest
{
    public string? SearchTerm { get; set; }
    public string? PeriodType { get; set; }
    public bool? IsOverdue { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? SortBy { get; set; } = "NextExecutionDate";
    public bool Descending { get; set; } = false;
}
