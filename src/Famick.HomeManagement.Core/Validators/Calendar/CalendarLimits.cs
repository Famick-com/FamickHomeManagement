namespace Famick.HomeManagement.Core.Validators.Calendar;

/// <summary>
/// Bounds on how much calendar a single request may expand (issue #120). The clients ask
/// for at most a few months at a time; these leave room for that and refuse the rest.
/// </summary>
public static class CalendarLimits
{
    /// <summary>Widest range <c>GET calendar/events</c> accepts.</summary>
    public const int MaxEventRangeDays = 400;

    /// <summary>Largest <c>days</c> that <c>GET calendar/upcoming</c> accepts.</summary>
    public const int MaxUpcomingDays = 366;

    /// <summary>Widest free/busy or slot-search range, matching find-slots.</summary>
    public const int MaxAvailabilityRangeDays = 30;

    /// <summary>Most users one free/busy or slot-search request may ask about.</summary>
    public const int MaxAvailabilityUsers = 50;

    /// <summary>Most occurrences one recurring event expands to in a single request.</summary>
    public const int MaxOccurrencesPerEvent = 1000;

    /// <summary>Most occurrences one events response carries.</summary>
    public const int MaxOccurrencesPerResponse = 10000;
}
