using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Domain.Enums;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class CreateMealItemRequestValidator : AbstractValidator<CreateMealItemRequest>
{
    public CreateMealItemRequestValidator()
    {
        RuleFor(x => x.ItemType)
            .IsInEnum().WithMessage("Invalid item type");

        RuleFor(x => x.RecipeId)
            .NotNull().WithMessage("Recipe ID is required for recipe items")
            .When(x => x.ItemType == MealItemType.Recipe);

        RuleFor(x => x.ProductId)
            .NotNull().WithMessage("Product ID is required for product items")
            .When(x => x.ItemType == MealItemType.Product);

        RuleFor(x => x.ProductQuantity)
            .GreaterThan(0).WithMessage("Product quantity must be greater than 0")
            .When(x => x.ItemType == MealItemType.Product && x.ProductQuantity.HasValue);

        RuleFor(x => x.FreetextDescription)
            .NotEmpty().WithMessage("Description is required for freetext items")
            .MaximumLength(500).WithMessage("Freetext description cannot exceed 500 characters")
            .When(x => x.ItemType == MealItemType.Freetext);
    }
}
