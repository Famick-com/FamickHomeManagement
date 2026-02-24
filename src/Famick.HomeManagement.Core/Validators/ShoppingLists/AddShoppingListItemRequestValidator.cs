using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ShoppingLists;

public class AddShoppingListItemRequestValidator : AbstractValidator<AddShoppingListItemRequest>
{
    public AddShoppingListItemRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Note));

        // ProductId, ProductName, Barcode, or Note must be provided
        // - ProductId: for products in local database
        // - ProductName: for products from store integrations not in local DB
        // - Barcode: for barcode-based lookup
        // - Note: for free-text items
        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue ||
                       !string.IsNullOrWhiteSpace(x.ProductName) ||
                       !string.IsNullOrWhiteSpace(x.Barcode) ||
                       !string.IsNullOrWhiteSpace(x.Note))
            .WithMessage("ProductId, ProductName, Barcode, or Note must be provided");
    }
}
