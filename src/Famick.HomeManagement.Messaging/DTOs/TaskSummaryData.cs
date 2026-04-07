using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class TaskSummaryData : IMessageData
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
    public int TotalTasks { get; set; }
    public int IncompleteTodos { get; set; }
    public int OverdueChores { get; set; }
    public int OverdueMaintenance { get; set; }

    public bool HasTodos => IncompleteTodos > 0;
    public bool HasChores => OverdueChores > 0;
    public bool HasMaintenance => OverdueMaintenance > 0;
}
