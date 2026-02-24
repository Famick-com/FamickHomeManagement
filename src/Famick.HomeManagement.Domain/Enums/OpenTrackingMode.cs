namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines how the remaining amount is tracked when a stock entry is marked as open.
/// </summary>
public enum OpenTrackingMode
{
    /// <summary>
    /// Amount represents a percentage remaining (0.0 to 1.0).
    /// Example: 0.75 means 75% of the original amount remains.
    /// </summary>
    Percentage = 0,

    /// <summary>
    /// Amount represents a literal count remaining.
    /// Example: 8 means 8 items remain (e.g., 8 slices of bread from a loaf of 12).
    /// </summary>
    Count = 1
}
