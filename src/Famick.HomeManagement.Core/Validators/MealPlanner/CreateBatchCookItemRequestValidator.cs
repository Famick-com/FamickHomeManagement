using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class CreateBatchCookItemRequestValidator : AbstractValidator<CreateBatchCookItemRequest>
{
    public CreateBatchCookItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required");

        RuleFor(x => x.TotalQuantity)
            .GreaterThan(0).WithMessage("Total quantity must be greater than zero")
            .When(x => x.TotalQuantity.HasValue);
    }
}
