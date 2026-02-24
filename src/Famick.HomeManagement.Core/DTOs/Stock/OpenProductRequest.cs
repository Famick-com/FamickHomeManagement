using System.ComponentModel.DataAnnotations;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to mark a stock entry as opened with tracking mode.
/// </summary>
public class OpenProductRequest
{
    /// <summary>
    /// How to track the remaining amount (percentage or count).
    /// </summary>
    [Required]
    public OpenTrackingMode TrackingMode { get; set; }

    /// <summary>
    /// The remaining amount after opening.
    /// For Percentage mode: 0.0 to 1.0 (e.g., 0.75 = 75% remaining)
    /// For Count mode: literal count remaining (e.g., 8 cookies left)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal RemainingAmount { get; set; }
}
