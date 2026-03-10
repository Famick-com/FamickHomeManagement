using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.ProductOnboarding;

public class ProductOnboardingCompleteRequestValidator : AbstractValidator<ProductOnboardingCompleteRequest>
{
    public ProductOnboardingCompleteRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotNull()
            .WithMessage("Questionnaire answers are required.");
    }
}
