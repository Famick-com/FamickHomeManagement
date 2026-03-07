namespace Famick.HomeManagement.Mobile.Models;

public class ChoreSummaryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public DateTime? NextExecutionDate { get; set; }
    public string? AssignedToUserName { get; set; }
    public bool IsOverdue { get; set; }

    public string DueDisplay =>
        NextExecutionDate.HasValue
            ? NextExecutionDate.Value.ToLocalTime().ToString("MMM d, yyyy")
            : "No schedule";

    public Color DueColor =>
        IsOverdue
            ? Color.FromArgb("#D32F2F")
            : Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#999999")
                : Color.FromArgb("#888888");
}

public class ChoreDetailItem
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
    public DateTime? StartDate { get; set; }
    public Guid? NextExecutionAssignedToUserId { get; set; }
    public string? NextExecutionAssignedToUserName { get; set; }
    public DateTime? NextExecutionDate { get; set; }
    public bool ConsumeProductOnExecution { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? ProductAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsOverdue =>
        NextExecutionDate.HasValue && NextExecutionDate.Value < DateTime.UtcNow;
}

public class ChoreLogItem
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

    public string StatusDisplay =>
        Undone ? "Undone" : Skipped ? "Skipped" : "Done";

    public string DateDisplay =>
        TrackedTime?.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
        ?? CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
}

public class ExecuteChoreMobileRequest
{
    public DateTime? TrackedTime { get; set; }
    public Guid? DoneByUserId { get; set; }
}

public class CreateChoreMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "manually";
    public int? PeriodDays { get; set; }
    public DateTime? StartDate { get; set; }
    public bool TrackDateOnly { get; set; }
    public bool Rollover { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentConfig { get; set; }
}

public class UpdateChoreMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "manually";
    public int? PeriodDays { get; set; }
    public DateTime? StartDate { get; set; }
    public bool TrackDateOnly { get; set; }
    public bool Rollover { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentConfig { get; set; }
}
