using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Validators.Common;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class CreateRecipeStepRequestValidator : AbstractValidator<CreateRecipeStepRequest>
{
    public CreateRecipeStepRequestValidator()
    {
        RuleFor(x => x.Instructions)
            .NotEmpty().WithMessage("Instructions are required")
            .MaximumLength(10000).WithMessage("Instructions cannot exceed 10000 characters");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Step title cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Step description cannot exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.VideoUrl)
            .MustBeWebUrl()
            .When(x => !string.IsNullOrEmpty(x.VideoUrl));
    }
}
