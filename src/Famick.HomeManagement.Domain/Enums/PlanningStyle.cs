namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// User preference for how the meal planner displays and navigates.
/// </summary>
public enum PlanningStyle
{
    /// <summary>
    /// Day-by-day view (default for mobile)
    /// </summary>
    DayByDay = 0,

    /// <summary>
    /// Full week grid view (default for web)
    /// </summary>
    WeekAtAGlance = 1
}
