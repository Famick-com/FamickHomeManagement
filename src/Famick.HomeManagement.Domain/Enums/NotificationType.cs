namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines the types of notifications that can be sent to users.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Daily alert for expiring food items and low-stock products
    /// </summary>
    ExpiryLowStock = 1,

    /// <summary>
    /// Daily summary of pending tasks (todos, overdue chores, overdue maintenance)
    /// </summary>
    TaskSummary = 2,

    /// <summary>
    /// Feature announcements from Famick (cloud only)
    /// </summary>
    NewFeatures = 3,

    /// <summary>
    /// Reminder for an upcoming calendar event (only sent to Involved members)
    /// </summary>
    CalendarReminder = 4
}
