using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class UpdateRecipeStepRequestValidator : AbstractValidator<UpdateRecipeStepRequest>
{
    public UpdateRecipeStepRequestValidator()
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
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Video URL must be a valid URL")
            .When(x => !string.IsNullOrEmpty(x.VideoUrl));
    }
}
