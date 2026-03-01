using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class DietaryProfileDto
{
    public Guid ContactId { get; set; }
    public string? DietaryNotes { get; set; }
    public List<ContactAllergenDto> Allergens { get; set; } = new();
    public List<ContactDietaryPreferenceDto> DietaryPreferences { get; set; } = new();
}

public class ContactAllergenDto
{
    public Guid Id { get; set; }
    public AllergenType AllergenType { get; set; }
    public AllergenSeverity Severity { get; set; }
}

public class ContactDietaryPreferenceDto
{
    public Guid Id { get; set; }
    public DietaryPreference DietaryPreference { get; set; }
}

public class UpdateDietaryProfileRequest
{
    public string? DietaryNotes { get; set; }
    public List<UpdateContactAllergenRequest> Allergens { get; set; } = new();
    public List<DietaryPreference> DietaryPreferences { get; set; } = new();
}

public class UpdateContactAllergenRequest
{
    public AllergenType AllergenType { get; set; }
    public AllergenSeverity Severity { get; set; }
}
