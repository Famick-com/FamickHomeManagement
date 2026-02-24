using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to add a new stock entry.
/// </summary>
public class AddStockRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    public DateTime? BestBeforeDate { get; set; }

    public DateTime? PurchasedDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ShoppingLocationId { get; set; }

    public string? Note { get; set; }
}
