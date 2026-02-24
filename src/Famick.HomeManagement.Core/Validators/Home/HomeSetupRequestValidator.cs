using Famick.HomeManagement.Core.DTOs.Home;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Home;

public class HomeSetupRequestValidator : AbstractValidator<HomeSetupRequest>
{
    public HomeSetupRequestValidator()
    {
        RuleFor(x => x.Unit)
            .MaximumLength(50).WithMessage("Unit cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Unit));

        RuleFor(x => x.YearBuilt)
            .InclusiveBetween(1800, DateTime.Now.Year + 5)
            .WithMessage($"Year built must be between 1800 and {DateTime.Now.Year + 5}")
            .When(x => x.YearBuilt.HasValue);

        RuleFor(x => x.SquareFootage)
            .GreaterThan(0).WithMessage("Square footage must be greater than 0")
            .When(x => x.SquareFootage.HasValue);

        RuleFor(x => x.Bedrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bedrooms cannot be negative")
            .When(x => x.Bedrooms.HasValue);

        RuleFor(x => x.Bathrooms)
            .GreaterThanOrEqualTo(0).WithMessage("Bathrooms cannot be negative")
            .When(x => x.Bathrooms.HasValue);

        RuleFor(x => x.HoaName)
            .MaximumLength(255).WithMessage("HOA name cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.HoaName));

        RuleFor(x => x.HoaRulesLink)
            .MaximumLength(1000).WithMessage("HOA rules link cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.HoaRulesLink));

        RuleFor(x => x.AcFilterSizes)
            .MaximumLength(255).WithMessage("AC filter sizes cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.AcFilterSizes));

        RuleFor(x => x.SmokeCoDetectorBatteryType)
            .MaximumLength(50).WithMessage("Battery type cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.SmokeCoDetectorBatteryType));
    }
}
