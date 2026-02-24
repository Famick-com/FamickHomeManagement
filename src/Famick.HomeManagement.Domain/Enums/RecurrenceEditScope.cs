namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines the scope when editing or deleting a recurring calendar event.
/// </summary>
public enum RecurrenceEditScope
{
    /// <summary>
    /// Apply the change only to this single occurrence (creates an exception).
    /// </summary>
    ThisOccurrence = 1,

    /// <summary>
    /// Apply the change to this occurrence and all future occurrences (splits the series).
    /// </summary>
    ThisAndFuture = 2,

    /// <summary>
    /// Apply the change to the entire recurring series.
    /// </summary>
    EntireSeries = 3
}
