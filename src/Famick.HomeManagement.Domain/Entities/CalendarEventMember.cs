using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Join table linking a calendar event to a household member with their participation type.
/// </summary>
public class CalendarEventMember : BaseEntity
{
    /// <summary>
    /// The calendar event this membership belongs to.
    /// </summary>
    public Guid CalendarEventId { get; set; }

    /// <summary>
    /// The household member participating in the event.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// How the member participates: Involved (busy, gets reminders) or Aware (visible, free, no reminders).
    /// </summary>
    public ParticipationType ParticipationType { get; set; } = ParticipationType.Involved;

    #region Navigation Properties

    public virtual CalendarEvent? CalendarEvent { get; set; }

    public virtual User? User { get; set; }

    #endregion
}
