using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class UpdateDietaryProfileRequestValidator : AbstractValidator<UpdateDietaryProfileRequest>
{
    public UpdateDietaryProfileRequestValidator()
    {
        RuleFor(x => x.DietaryNotes)
            .MaximumLength(500).WithMessage("Dietary notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.DietaryNotes));

        RuleForEach(x => x.Allergens)
            .ChildRules(allergen =>
            {
                allergen.RuleFor(a => a.AllergenType)
                    .IsInEnum().WithMessage("Invalid allergen type");
                allergen.RuleFor(a => a.Severity)
                    .IsInEnum().WithMessage("Invalid allergen severity");
            });

        RuleForEach(x => x.DietaryPreferences)
            .IsInEnum().WithMessage("Invalid dietary preference");
    }
}
