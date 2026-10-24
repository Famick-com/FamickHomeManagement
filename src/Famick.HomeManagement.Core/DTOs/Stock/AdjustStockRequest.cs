using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to adjust an existing stock entry's amount or details.
/// </summary>
/// <remarks>
/// A null <see cref="BestBeforeDate"/>, <see cref="LocationId"/> or <see cref="Note"/> leaves that
/// field as it is. Removing a date or a location takes the matching Clear flag, because existing
/// callers send null to mean "unchanged" and must keep doing so.
/// </remarks>
public class AdjustStockRequest : IValidatableObject
{
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Amount must be 0 or greater")]
    public decimal Amount { get; set; }

    public DateTime? BestBeforeDate { get; set; }

    public Guid? LocationId { get; set; }

    public string? Note { get; set; }

    /// <summary>Removes the entry's best-before date. Cannot be combined with <see cref="BestBeforeDate"/>.</summary>
    public bool ClearBestBeforeDate { get; set; }

    /// <summary>Removes the entry's location. Cannot be combined with <see cref="LocationId"/>.</summary>
    public bool ClearLocation { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClearBestBeforeDate && BestBeforeDate.HasValue)
        {
            yield return new ValidationResult(
                "Set a best-before date or clear it, not both",
                [nameof(BestBeforeDate), nameof(ClearBestBeforeDate)]);
        }

        if (ClearLocation && LocationId.HasValue)
        {
            yield return new ValidationResult(
                "Set a location or clear it, not both",
                [nameof(LocationId), nameof(ClearLocation)]);
        }
    }
}
