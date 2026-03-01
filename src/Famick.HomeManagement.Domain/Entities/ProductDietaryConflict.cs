using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tags a product with a dietary preference it conflicts with
/// (e.g., a product containing meat conflicts with Vegetarian).
/// </summary>
public class ProductDietaryConflict : BaseEntity
{
    public Guid ProductId { get; set; }

    public DietaryPreference DietaryPreference { get; set; }

    // Navigation properties
    public virtual Product Product { get; set; } = null!;
}
