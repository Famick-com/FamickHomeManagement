using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLists;

public class UpdateShoppingListRequestValidator : AbstractValidator<UpdateShoppingListRequest>
{
    public UpdateShoppingListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Shopping list name is required")
            .MaximumLength(200).WithMessage("Shopping list name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
