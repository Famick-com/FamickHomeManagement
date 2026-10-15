using Famick.HomeManagement.Core.Validators.Calendar;
using Famick.HomeManagement.Domain.Entities;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// The one place a stored RRULE is parsed and expanded (issue #120). A rule outside
/// <see cref="RecurrenceRuleCheck"/>, or one Ical.Net cannot read, is logged and skipped
/// so it cannot break the rest of the household's calendar, feed or reminders, and no
/// event expands past <see cref="CalendarLimits.MaxOccurrencesPerEvent"/>.
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// The event's rule as a pattern, or null (logged) when it is refused or malformed.
    /// </summary>
    public static RecurrencePattern? TryParse(CalendarEvent evt, ILogger logger)
    {
        var problem = RecurrenceRuleCheck.Problem(evt.RecurrenceRule!);
        if (problem == null)
        {
            try
            {
                return new RecurrencePattern(evt.RecurrenceRule!);
            }
            catch (Exception ex)
            {
                problem = ex.Message;
            }
        }

        logger.LogWarning("Skipping recurrence of calendar event {EventId}: {Problem}", evt.Id, problem);
        return null;
    }

    /// <summary>
    /// UTC start times of the event's occurrences from <paramref name="from"/> until
    /// <paramref name="before"/>, ending at its RecurrenceEndDate. Empty when the rule is
    /// skipped; cut at <see cref="CalendarLimits.MaxOccurrencesPerEvent"/>.
    /// </summary>
    public static List<DateTime> OccurrenceStarts(CalendarEvent evt, DateTime from, DateTime before, ILogger logger)
    {
        var pattern = TryParse(evt, logger);
        if (pattern == null)
            return [];

        var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
        {
            DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
            DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC")
        };
        icalEvent.RecurrenceRules.Add(pattern);
        new Calendar().Events.Add(icalEvent);

        var starts = new List<DateTime>();
        try
        {
            foreach (var occurrence in icalEvent.GetOccurrences(new CalDateTime(from, "UTC"))
                         .TakeWhileBefore(new CalDateTime(before, "UTC")))
            {
                var start = occurrence.Period.StartTime.AsUtc;
                if (evt.RecurrenceEndDate.HasValue && start > evt.RecurrenceEndDate.Value)
                    break;

                if (starts.Count == CalendarLimits.MaxOccurrencesPerEvent)
                {
                    logger.LogWarning("Calendar event {EventId} has more than {Max} occurrences in range; cut there",
                        evt.Id, CalendarLimits.MaxOccurrencesPerEvent);
                    break;
                }

                starts.Add(start);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping recurrence of calendar event {EventId}: expansion failed", evt.Id);
            return [];
        }

        return starts;
    }
}
