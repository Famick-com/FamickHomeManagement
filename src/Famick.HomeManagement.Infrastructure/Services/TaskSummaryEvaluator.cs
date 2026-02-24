using Famick.HomeManagement.Core.Interfaces;
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

    public NotificationType Type => NotificationType.TaskSummary;

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
        {
            return Array.Empty<NotificationItem>();
        }

        // Build notification content
        var parts = new List<string>();
        if (incompleteTodos > 0) parts.Add($"{incompleteTodos} todo(s)");
        if (overdueChoreCount > 0) parts.Add($"{overdueChoreCount} overdue chore(s)");
        if (overdueMaintenanceCount > 0) parts.Add($"{overdueMaintenanceCount} vehicle maintenance due");

        var title = $"You have {totalTasks} pending task(s)";
        var summary = string.Join(", ", parts);

        var emailHtml = BuildEmailHtml(incompleteTodos, overdueChoreCount, overdueMaintenanceCount);
        var emailText = $"TASK SUMMARY\n\n{summary}\n\nVisit your Famick dashboard to view and manage your tasks.";

        // Send to all users in the tenant
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return users.Select(userId => new NotificationItem(
            userId,
            NotificationType.TaskSummary,
            title,
            summary,
            "/todos",
            $"Famick: {title}",
            emailHtml,
            emailText
        )).ToList();
    }

    private static string BuildEmailHtml(int todos, int chores, int maintenance)
    {
        var html = "<h2>Daily Task Summary</h2><ul>";
        if (todos > 0) html += $"<li><strong>{todos}</strong> incomplete todo item(s)</li>";
        if (chores > 0) html += $"<li><strong>{chores}</strong> overdue chore(s)</li>";
        if (maintenance > 0) html += $"<li><strong>{maintenance}</strong> vehicle maintenance due</li>";
        html += "</ul><p>Visit your Famick dashboard to view and manage your tasks.</p>";
        return html;
    }
}
