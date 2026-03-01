namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a weekly meal plan. One plan per tenant per week.
/// Uses PostgreSQL xmin column for optimistic concurrency.
/// </summary>
public class MealPlan : BaseTenantEntity
{
    /// <summary>
    /// The Monday that starts this plan week.
    /// </summary>
    public DateOnly WeekStartDate { get; set; }

    /// <summary>
    /// The user who last modified this plan.
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// Optimistic concurrency token mapped to PostgreSQL xmin system column.
    /// </summary>
    public uint Version { get; set; }

    // Navigation properties
    public virtual User? UpdatedByUser { get; set; }
    public virtual ICollection<MealPlanEntry> Entries { get; set; } = new List<MealPlanEntry>();
}
