namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks which feature tips a user has dismissed in the meal planner.
/// </summary>
public class UserMealPlannerTip : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Unique key identifying the tip (e.g., "batch-cooking", "drag-and-drop").
    /// </summary>
    public string TipKey { get; set; } = string.Empty;

    /// <summary>
    /// When the user dismissed this tip.
    /// </summary>
    public DateTime DismissedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
}
