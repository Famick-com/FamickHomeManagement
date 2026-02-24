using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLists;

public class AddToShoppingListRequestValidator : AbstractValidator<AddToShoppingListRequest>
{
    public AddToShoppingListRequestValidator()
    {
        RuleFor(x => x.ShoppingListId)
            .NotEmpty().WithMessage("Shopping list is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue || !string.IsNullOrWhiteSpace(x.Barcode) || !string.IsNullOrWhiteSpace(x.ProductName))
            .WithMessage("Either ProductId, Barcode, or ProductName must be provided");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Note));
    }
}
