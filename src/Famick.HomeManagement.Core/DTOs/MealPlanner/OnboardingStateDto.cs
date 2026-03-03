using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class OnboardingStateDto
{
    public bool HasCompletedOnboarding { get; set; }
    public PlanningStyle? PlanningStyle { get; set; }
    public List<Guid> CollapsedMealTypeIds { get; set; } = new();
}

public class SaveOnboardingRequest
{
    public PlanningStyle? PlanningStyle { get; set; }
    public List<Guid>? CollapsedMealTypeIds { get; set; }
    public List<OnboardingMealTypeSelection>? MealTypes { get; set; }
}

public class OnboardingMealTypeSelection
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class FeatureTipDto
{
    public string TipKey { get; set; } = string.Empty;
    public bool IsDismissed { get; set; }
}
