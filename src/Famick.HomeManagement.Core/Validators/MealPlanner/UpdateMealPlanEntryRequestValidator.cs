using Famick.HomeManagement.Core.DTOs.MealPlanner;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.MealPlanner;

public class UpdateMealPlanEntryRequestValidator : AbstractValidator<UpdateMealPlanEntryRequest>
{
    public UpdateMealPlanEntryRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.MealId.HasValue && string.IsNullOrEmpty(x.InlineNote)) ||
                       (!x.MealId.HasValue && !string.IsNullOrEmpty(x.InlineNote)))
            .WithMessage("Exactly one of MealId or InlineNote must be provided");

        RuleFor(x => x.InlineNote)
            .MaximumLength(200).WithMessage("Inline note cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.InlineNote));

        RuleFor(x => x.MealTypeId)
            .NotEmpty().WithMessage("Meal type is required");

        RuleFor(x => x.DayOfWeek)
            .InclusiveBetween(0, 6).WithMessage("Day of week must be between 0 (Monday) and 6 (Sunday)");

        RuleFor(x => x)
            .Must(x => !(x.IsBatchSource && x.BatchSourceEntryId.HasValue))
            .WithMessage("An entry cannot be both a batch source and reference a batch source");

        RuleFor(x => x)
            .Must(x => (!x.IsBatchSource && !x.BatchSourceEntryId.HasValue) || x.MealId.HasValue)
            .WithMessage("Batch cooking fields require a meal reference");
    }
}
