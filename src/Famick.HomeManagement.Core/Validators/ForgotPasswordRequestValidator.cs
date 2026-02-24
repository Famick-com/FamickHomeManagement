using Famick.HomeManagement.Core.DTOs.Authentication;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators;

/// <summary>
/// Validator for ForgotPasswordRequest
/// </summary>
public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}
