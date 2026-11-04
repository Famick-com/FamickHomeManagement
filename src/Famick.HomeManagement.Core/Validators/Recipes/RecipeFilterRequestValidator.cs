using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Mapping;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class RecipeFilterRequestValidator : AbstractValidator<RecipeFilterRequest>
{
    public RecipeFilterRequestValidator()
    {
        RuleFor(x => x.MinRating!.Value)
            .Must(RecipeRatings.IsValid)
            .WithName(nameof(RecipeFilterRequest.MinRating))
            .WithMessage($"Minimum rating must be from {RecipeRatings.MinStars} to {RecipeRatings.MaxStars} stars, in half stars")
            .When(x => x.MinRating.HasValue);
    }
}
