using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class CreateRecipeIngredientRequestValidator : AbstractValidator<CreateRecipeIngredientRequest>
{
    public CreateRecipeIngredientRequestValidator()
    {
        // A line is a product, or text that isn't linked to one yet.
        RuleFor(x => x.ProductId)
            .Must(id => id.HasValue && id.Value != Guid.Empty).WithMessage("Product ID is required")
            .When(x => x.ProductId.HasValue || string.IsNullOrWhiteSpace(x.OriginalText));

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0")
            .When(x => x.ProductId.HasValue);

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Amount cannot be negative")
            .When(x => !x.ProductId.HasValue);

        RuleFor(x => x.OriginalText)
            .MaximumLength(500).WithMessage("Original text cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.OriginalText));

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Note));

        RuleFor(x => x.IngredientGroup)
            .MaximumLength(100).WithMessage("Ingredient group cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.IngredientGroup));
    }
}
