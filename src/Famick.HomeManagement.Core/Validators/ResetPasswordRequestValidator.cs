using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Validators.Common;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators;

/// <summary>
/// Validator for ResetPasswordRequest
/// </summary>
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MustBeAcceptablePassword();

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match");
    }
}
