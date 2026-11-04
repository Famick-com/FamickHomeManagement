using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Mapping;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class SetRecipeRatingRequestValidator : AbstractValidator<SetRecipeRatingRequest>
{
    public SetRecipeRatingRequestValidator()
    {
        RuleFor(x => x.Rating)
            .Must(RecipeRatings.IsValid)
            .WithMessage($"Rating must be from {RecipeRatings.MinStars} to {RecipeRatings.MaxStars} stars, in half stars");
    }
}
