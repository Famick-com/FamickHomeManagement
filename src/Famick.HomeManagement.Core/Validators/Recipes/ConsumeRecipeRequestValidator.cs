using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Validators.Common;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class ConsumeRecipeRequestValidator : AbstractValidator<ConsumeRecipeRequest>
{
    /// <summary>More lines than any recipe has, nested ones included.</summary>
    public const int MaxLines = 500;

    public ConsumeRecipeRequestValidator()
    {
        RuleFor(x => x.Servings)
            .InclusiveBetween(1, QueryLimits.MaxServings).WithMessage($"Servings must be between 1 and {QueryLimits.MaxServings}")
            .When(x => x.Servings.HasValue);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("At least one ingredient is required")
            .Must(l => l.Count <= MaxLines).WithMessage($"At most {MaxLines} ingredients can be consumed at once");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Product ID is required");
            line.RuleFor(l => l.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0");
        });
    }
}
