using Famick.HomeManagement.Core.DTOs.Chores;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Chores;

public class SkipChoreRequestValidator : AbstractValidator<SkipChoreRequest>
{
    public SkipChoreRequestValidator()
    {
        // ScheduledExecutionTime is optional
        // No required validations needed
    }
}
