using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class UpdateMealTypeRequestValidator : AbstractValidator<UpdateMealTypeRequest>
{
    public UpdateMealTypeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Meal type name is required")
            .MaximumLength(100).WithMessage("Meal type name cannot exceed 100 characters");

        RuleFor(x => x.Color)
            .MaximumLength(50).WithMessage("Color cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Color));
    }
}
