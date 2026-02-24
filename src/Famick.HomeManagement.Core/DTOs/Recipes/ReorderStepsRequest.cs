namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to reorder steps within a recipe.
/// </summary>
public class ReorderStepsRequest
{
    /// <summary>
    /// Ordered list of step IDs representing the new order.
    /// </summary>
    public List<Guid> StepIds { get; set; } = new();
}
