using Famick.HomeManagement.Core.DTOs.Chores;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Chores;

public class UpdateChoreRequestValidator : AbstractValidator<UpdateChoreRequest>
{
    private static readonly string[] ValidPeriodTypes =
        { "manually", "dynamic-regular", "daily", "weekly", "monthly" };

    public UpdateChoreRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Chore name is required")
            .MaximumLength(200).WithMessage("Chore name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.PeriodType)
            .Must(pt => ValidPeriodTypes.Contains(pt))
            .WithMessage($"PeriodType must be one of: {string.Join(", ", ValidPeriodTypes)}");

        RuleFor(x => x.PeriodDays)
            .GreaterThan(0).WithMessage("PeriodDays must be greater than 0")
            .When(x => x.PeriodType == "dynamic-regular" || x.PeriodType == "monthly");

        RuleFor(x => x.PeriodDays)
            .NotNull().WithMessage("PeriodDays is required for dynamic-regular and monthly period types")
            .When(x => x.PeriodType == "dynamic-regular" || x.PeriodType == "monthly");

        // Product consumption validation
        RuleFor(x => x.ProductId)
            .NotNull().WithMessage("ProductId is required when ConsumeProductOnExecution is true")
            .When(x => x.ConsumeProductOnExecution);

        RuleFor(x => x.ProductAmount)
            .NotNull().WithMessage("ProductAmount is required when ConsumeProductOnExecution is true")
            .GreaterThan(0).WithMessage("ProductAmount must be greater than 0")
            .When(x => x.ConsumeProductOnExecution);
    }
}
