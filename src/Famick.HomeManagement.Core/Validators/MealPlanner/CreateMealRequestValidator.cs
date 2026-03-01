using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Domain.Enums;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class CreateMealRequestValidator : AbstractValidator<CreateMealRequest>
{
    public CreateMealRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Meal name is required")
            .MaximumLength(200).WithMessage("Meal name cannot exceed 200 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateMealItemRequestValidator());
    }
}
