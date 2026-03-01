using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class GenerateShoppingListRequestValidator : AbstractValidator<GenerateShoppingListRequest>
{
    public GenerateShoppingListRequestValidator()
    {
        RuleFor(x => x.ShoppingListId)
            .NotEmpty().WithMessage("Shopping list ID is required");
    }
}
