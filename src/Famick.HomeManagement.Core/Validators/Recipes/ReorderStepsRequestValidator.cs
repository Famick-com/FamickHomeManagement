using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class ReorderStepsRequestValidator : AbstractValidator<ReorderStepsRequest>
{
    public ReorderStepsRequestValidator()
    {
        RuleFor(x => x.StepIds)
            .NotEmpty().WithMessage("Step IDs list cannot be empty");
    }
}
