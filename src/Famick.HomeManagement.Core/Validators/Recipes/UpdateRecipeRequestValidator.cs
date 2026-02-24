using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class UpdateRecipeRequestValidator : AbstractValidator<UpdateRecipeRequest>
{
    public UpdateRecipeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Recipe name is required")
            .MaximumLength(200).WithMessage("Recipe name cannot exceed 200 characters");

        RuleFor(x => x.Source)
            .MaximumLength(2000).WithMessage("Source cannot exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Source));

        RuleFor(x => x.Servings)
            .GreaterThan(0).WithMessage("Servings must be greater than 0");

        RuleFor(x => x.Attribution)
            .MaximumLength(1000).WithMessage("Attribution cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Attribution));
    }
}
