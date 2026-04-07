namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines all message types that can be sent through the unified messaging service.
/// Notification types (1-99) are stored in the database and respect user channel preferences.
/// Transactional types (100+) bypass user preferences and are email-only.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Daily alert for expiring food items
    /// </summary>
    Expiry = 1,

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
    CalendarReminder = 4,

    /// <summary>
    /// Daily alert for products below minimum stock level
    /// </summary>
    LowStock = 5,

    /// <summary>
    /// Email verification for new user registration
    /// </summary>
    EmailVerification = 100,

    /// <summary>
    /// Password reset request email
    /// </summary>
    PasswordReset = 101,

    /// <summary>
    /// Password change confirmation email
    /// </summary>
    PasswordChanged = 102,

    /// <summary>
    /// Welcome email for admin-created users
    /// </summary>
    Welcome = 103
}
