using FluentValidation;
using FluentValidation.Results;

namespace Famick.HomeManagement.Core.Validators.Common;

/// <summary>
/// The one rule a chosen password has to meet, wherever it is set: registration, accepting an
/// invitation, a reset link, changing your own, and an admin creating or resetting a member.
/// </summary>
/// <remarks>
/// Applied in the services that hash a password, not only at the edge, so a new endpoint or a
/// client that skips its own check cannot set a weaker one. Generated passwords are longer than
/// the minimum and are not passed through it.
/// </remarks>
public static class PasswordRules
{
    public const int MinimumLength = 12;

    // A literal so attributes can use it; keep it in step with MinimumLength.
    public const string TooShortMessage = "Password must be at least 12 characters";

    public static bool IsAcceptable(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;

    /// <summary>
    /// Throws a validation error (a 400 naming <paramref name="propertyName"/>) unless
    /// <paramref name="password"/> meets the rule.
    /// </summary>
    public static void Ensure(string? password, string propertyName)
    {
        if (!IsAcceptable(password))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(propertyName, TooShortMessage),
            });
        }
    }

    /// <summary>Requires a password that meets the rule.</summary>
    public static IRuleBuilderOptions<T, string> MustBeAcceptablePassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsAcceptable).WithMessage(TooShortMessage);
}
