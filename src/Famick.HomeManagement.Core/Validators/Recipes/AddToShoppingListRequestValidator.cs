using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class AddToShoppingListRequestValidator : AbstractValidator<AddToShoppingListRequest>
{
    public AddToShoppingListRequestValidator()
    {
        RuleFor(x => x.ShoppingListId)
            .NotEmpty().WithMessage("Shopping list ID is required");

        RuleFor(x => x.Servings)
            .GreaterThan(0).WithMessage("Servings must be greater than 0")
            .When(x => x.Servings.HasValue);
    }
}
