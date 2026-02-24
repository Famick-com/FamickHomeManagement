using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to adjust an existing stock entry's amount or details.
/// </summary>
public class AdjustStockRequest
{
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Amount must be 0 or greater")]
    public decimal Amount { get; set; }

    public DateTime? BestBeforeDate { get; set; }

    public Guid? LocationId { get; set; }

    public string? Note { get; set; }
}
