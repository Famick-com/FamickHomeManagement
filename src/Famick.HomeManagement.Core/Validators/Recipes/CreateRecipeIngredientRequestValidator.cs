using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class CreateRecipeIngredientRequestValidator : AbstractValidator<CreateRecipeIngredientRequest>
{
    public CreateRecipeIngredientRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Note));

        RuleFor(x => x.IngredientGroup)
            .MaximumLength(100).WithMessage("Ingredient group cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.IngredientGroup));
    }
}
