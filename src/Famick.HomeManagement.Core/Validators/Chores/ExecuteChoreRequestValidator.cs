using Famick.HomeManagement.Core.DTOs.Chores;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Chores;

public class ExecuteChoreRequestValidator : AbstractValidator<ExecuteChoreRequest>
{
    public ExecuteChoreRequestValidator()
    {
        // TrackedTime is optional - defaults to current time on the server
        // DoneByUserId is optional - defaults to current user on the server
        // No required validations needed
    }
}
