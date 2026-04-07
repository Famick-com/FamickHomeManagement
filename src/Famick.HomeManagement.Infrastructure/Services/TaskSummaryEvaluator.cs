using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Evaluates pending tasks (todos, overdue chores, overdue vehicle maintenance) for a tenant.
/// Groups results per user and produces one notification per user.
/// </summary>
public class TaskSummaryEvaluator : INotificationEvaluator
{
    private readonly HomeManagementDbContext _db;
    private readonly ILogger<TaskSummaryEvaluator> _logger;

    public MessageType Type => MessageType.TaskSummary;

    public TaskSummaryEvaluator(
        HomeManagementDbContext db,
        ILogger<TaskSummaryEvaluator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationItem>> EvaluateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        // Incomplete TodoItems
        var incompleteTodos = await _db.TodoItems
            .Where(t => t.TenantId == tenantId && !t.IsCompleted)
            .CountAsync(cancellationToken);

        // Overdue periodic chores (exclude manually triggered)
        var overdueChores = await _db.Chores
            .Include(c => c.LogEntries)
            .Where(c => c.TenantId == tenantId && c.PeriodType != "manually" && c.PeriodDays != null)
            .ToListAsync(cancellationToken);

        var overdueChoreCount = overdueChores.Count(c =>
        {
            var lastLog = c.LogEntries?
                .Where(l => !l.Undone && !l.Skipped && l.TrackedTime.HasValue)
                .OrderByDescending(l => l.TrackedTime)
                .FirstOrDefault();

            if (lastLog?.TrackedTime is null) return true; // Never executed = overdue
            return lastLog.TrackedTime.Value.Date.AddDays(c.PeriodDays!.Value) <= today;
        });

        // Overdue vehicle maintenance schedules
        var overdueMaintenanceCount = await _db.VehicleMaintenanceSchedules
            .Where(s => s.TenantId == tenantId
                && s.IsActive
                && s.NextDueDate != null
                && s.NextDueDate <= today)
            .CountAsync(cancellationToken);

        var totalTasks = incompleteTodos + overdueChoreCount + overdueMaintenanceCount;

        if (totalTasks == 0)
            return Array.Empty<NotificationItem>();

        var parts = new List<string>();
        if (incompleteTodos > 0) parts.Add($"{incompleteTodos} todo(s)");
        if (overdueChoreCount > 0) parts.Add($"{overdueChoreCount} overdue chore(s)");
        if (overdueMaintenanceCount > 0) parts.Add($"{overdueMaintenanceCount} vehicle maintenance due");

        var title = $"You have {totalTasks} pending task(s)";
        var summary = string.Join(", ", parts);

        var data = new TaskSummaryData
        {
            Title = title,
            Summary = summary,
            DeepLinkUrl = "/todos",
            TotalTasks = totalTasks,
            IncompleteTodos = incompleteTodos,
            OverdueChores = overdueChoreCount,
            OverdueMaintenance = overdueMaintenanceCount
        };

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return users.Select(userId => new NotificationItem(
            userId,
            MessageType.TaskSummary,
            title,
            summary,
            "/todos",
            data
        )).ToList();
    }
}
