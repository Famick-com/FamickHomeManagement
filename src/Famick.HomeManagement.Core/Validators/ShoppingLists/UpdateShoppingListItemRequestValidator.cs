using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLists;

public class UpdateShoppingListItemRequestValidator : AbstractValidator<UpdateShoppingListItemRequest>
{
    public UpdateShoppingListItemRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Note));
    }
}
