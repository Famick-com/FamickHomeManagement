using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tags a product with a specific allergen it contains.
/// </summary>
public class ProductAllergen : BaseEntity
{
    public Guid ProductId { get; set; }

    public AllergenType AllergenType { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
}
