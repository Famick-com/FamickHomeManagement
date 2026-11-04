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

        RuleFor(x => x.Shortfalls)
            .Must(s => s!.Count <= ConsumeRecipeRequestValidator.MaxLines)
            .WithMessage($"At most {ConsumeRecipeRequestValidator.MaxLines} shortfalls can be added at once")
            .When(x => x.Shortfalls != null);

        RuleForEach(x => x.Shortfalls).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Product ID is required");
            line.RuleFor(l => l.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0");
        });
    }
}
