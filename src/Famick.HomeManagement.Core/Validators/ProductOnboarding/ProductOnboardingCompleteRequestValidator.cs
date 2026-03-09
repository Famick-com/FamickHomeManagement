using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ProductOnboarding;

public class ProductOnboardingCompleteRequestValidator : AbstractValidator<ProductOnboardingCompleteRequest>
{
    public ProductOnboardingCompleteRequestValidator()
    {
        RuleFor(x => x.SelectedMasterProductIds)
            .NotEmpty()
            .WithMessage("At least one product must be selected.");

        RuleFor(x => x.Answers)
            .NotNull()
            .WithMessage("Questionnaire answers are required.");
    }
}
