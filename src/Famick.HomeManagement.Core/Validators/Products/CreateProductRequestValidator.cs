using Famick.HomeManagement.Core.DTOs.Products;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Products;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.LocationId)
            .NotEmpty().WithMessage("Location is required");

        RuleFor(x => x.QuantityUnitIdPurchase)
            .NotEmpty().WithMessage("Purchase quantity unit is required");

        RuleFor(x => x.QuantityUnitIdStock)
            .NotEmpty().WithMessage("Stock quantity unit is required");

        RuleFor(x => x.QuantityUnitFactorPurchaseToStock)
            .GreaterThan(0).WithMessage("Purchase to stock conversion factor must be greater than 0");

        RuleFor(x => x.MinStockAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock amount cannot be negative");

        RuleFor(x => x.DefaultBestBeforeDays)
            .GreaterThanOrEqualTo(0).WithMessage("Default best before days cannot be negative");
    }
}
