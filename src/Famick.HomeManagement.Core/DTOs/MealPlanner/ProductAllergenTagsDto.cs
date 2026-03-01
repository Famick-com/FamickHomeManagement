using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class ProductAllergenTagsDto
{
    public Guid ProductId { get; set; }
    public List<AllergenType> Allergens { get; set; } = new();
    public List<DietaryPreference> DietaryConflicts { get; set; } = new();
}

public class UpdateProductAllergenTagsRequest
{
    public List<AllergenType> Allergens { get; set; } = new();
    public List<DietaryPreference> DietaryConflicts { get; set; } = new();
}
