namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines how a household member participates in a calendar event.
/// </summary>
public enum ParticipationType
{
    /// <summary>
    /// Member is actively involved - the event blocks their availability and they receive reminders.
    /// </summary>
    Involved = 1,

    /// <summary>
    /// Member is aware of the event - it appears on their calendar but does not block availability or trigger reminders.
    /// </summary>
    Aware = 2
}
