namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines the types of tasks that can be tracked in the TODO system.
/// </summary>
public enum TaskType
{
    /// <summary>
    /// Product needs inventory setup (e.g., new product from shopping)
    /// </summary>
    Inventory = 1,

    /// <summary>
    /// Product needs details completed
    /// </summary>
    Product = 2,

    /// <summary>
    /// Equipment maintenance follow-up
    /// </summary>
    Equipment = 3,

    /// <summary>
    /// General task
    /// </summary>
    Other = 99
}
