using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Per-user meal planner preferences and onboarding state.
/// </summary>
public class UserMealPlannerPreference : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the user has completed the meal planner onboarding.
    /// </summary>
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// Preferred planning style (day-by-day or week-at-a-glance).
    /// </summary>
    public PlanningStyle? PlanningStyle { get; set; }

    /// <summary>
    /// JSON array of meal type IDs that the user has collapsed in the planner view.
    /// </summary>
    public string? CollapsedMealTypeIds { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
