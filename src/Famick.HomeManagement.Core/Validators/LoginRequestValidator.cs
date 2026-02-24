using Famick.HomeManagement.Core.DTOs.Authentication;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators;

/// <summary>
/// Validator for LoginRequest
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
