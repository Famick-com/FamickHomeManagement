using Famick.HomeManagement.Core.DTOs.Home;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Home;

public class UpdateHomeRequestValidator : AbstractValidator<UpdateHomeRequest>
{
    public UpdateHomeRequestValidator()
    {
        // Property Basics
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

        // HVAC
        RuleFor(x => x.AcFilterSizes)
            .MaximumLength(255).WithMessage("AC filter sizes cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.AcFilterSizes));

        // Maintenance
        RuleFor(x => x.AcFilterReplacementIntervalDays)
            .GreaterThan(0).WithMessage("AC filter replacement interval must be greater than 0")
            .When(x => x.AcFilterReplacementIntervalDays.HasValue);

        RuleFor(x => x.FridgeWaterFilterType)
            .MaximumLength(100).WithMessage("Fridge water filter type cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.FridgeWaterFilterType));

        RuleFor(x => x.UnderSinkFilterType)
            .MaximumLength(100).WithMessage("Under sink filter type cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.UnderSinkFilterType));

        RuleFor(x => x.WholeHouseFilterType)
            .MaximumLength(100).WithMessage("Whole house filter type cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.WholeHouseFilterType));

        RuleFor(x => x.SmokeCoDetectorBatteryType)
            .MaximumLength(50).WithMessage("Battery type cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.SmokeCoDetectorBatteryType));

        // Insurance
        RuleFor(x => x.InsurancePolicyNumber)
            .MaximumLength(100).WithMessage("Policy number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.InsurancePolicyNumber));

        RuleFor(x => x.InsuranceAgentName)
            .MaximumLength(255).WithMessage("Agent name cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.InsuranceAgentName));

        RuleFor(x => x.InsuranceAgentPhone)
            .MaximumLength(50).WithMessage("Agent phone cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.InsuranceAgentPhone));

        RuleFor(x => x.InsuranceAgentEmail)
            .MaximumLength(255).WithMessage("Agent email cannot exceed 255 characters")
            .EmailAddress().WithMessage("Agent email must be a valid email address")
            .When(x => !string.IsNullOrEmpty(x.InsuranceAgentEmail));

        RuleFor(x => x.PropertyTaxAccountNumber)
            .MaximumLength(100).WithMessage("Property tax account number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.PropertyTaxAccountNumber));

        RuleFor(x => x.AppraisalValue)
            .GreaterThan(0).WithMessage("Appraisal value must be greater than 0")
            .When(x => x.AppraisalValue.HasValue);
    }
}
