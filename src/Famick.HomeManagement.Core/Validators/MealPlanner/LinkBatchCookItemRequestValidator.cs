using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class LinkBatchCookItemRequestValidator : AbstractValidator<LinkBatchCookItemRequest>
{
    public LinkBatchCookItemRequestValidator()
    {
        RuleFor(x => x.BatchCookItemId)
            .NotEmpty().WithMessage("Batch cook item is required");

        RuleFor(x => x.QuantityUsed)
            .GreaterThan(0).WithMessage("Quantity used must be greater than zero")
            .When(x => x.QuantityUsed.HasValue);
    }
}
