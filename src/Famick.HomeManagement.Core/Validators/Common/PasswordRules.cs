using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;

namespace Famick.HomeManagement.Core.Validators.Common;

/// <summary>
/// The rule a chosen password has to meet, wherever it is set: registration, accepting an
/// invitation, a reset link, changing your own, and an admin creating or resetting a member.
/// At least <see cref="MinimumLength"/> characters, at least one digit, and at least one ASCII
/// punctuation character (<c>!"#$%&amp;'()*+,-./:;&lt;=&gt;?@[\]^_`{|}~</c>).
/// </summary>
/// <remarks>
/// Applied in the services that hash a password, not only at the edge, so a new endpoint or a
/// client that skips its own check cannot set a weaker one. Passwords made by
/// <see cref="Generate"/> always meet it.
/// </remarks>
public static class PasswordRules
{
    public const int MinimumLength = 12;

    // A literal so attributes can use it; keep it in step with the rule.
    public const string Message =
        "Password must be at least 12 characters and include a number and a special character (such as ! @ # $)";

    private const int GeneratedLength = 16;
    private const string Letters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Specials = "!@#$%";

    public static bool IsAcceptable(string? password) =>
        password is not null
        && password.Length >= MinimumLength
        && password.Any(char.IsAsciiDigit)
        && password.Any(IsSpecial);

    /// <summary>ASCII punctuation and symbols: printable, and not a letter, digit or space.</summary>
    public static bool IsSpecial(char c) => c is > ' ' and <= '~' && !char.IsAsciiLetterOrDigit(c);

    /// <summary>
    /// Throws a validation error (a 400 naming <paramref name="propertyName"/>) unless
    /// <paramref name="password"/> meets the rule.
    /// </summary>
    public static void Ensure(string? password, string propertyName)
    {
        if (!IsAcceptable(password))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new ValidationFailure(propertyName, Message),
            });
        }
    }

    /// <summary>Requires a password that meets the rule.</summary>
    public static IRuleBuilderOptions<T, string> MustBeAcceptablePassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsAcceptable).WithMessage(Message);

    /// <summary>
    /// A random password that meets the rule, for an admin to hand over. Leaves out characters
    /// that are easy to misread (0/O, 1/l/I).
    /// </summary>
    public static string Generate()
    {
        const string all = Letters + Digits + Specials;
        var chars = new char[GeneratedLength];
        chars[0] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[1] = Specials[RandomNumberGenerator.GetInt32(Specials.Length)];
        for (var i = 2; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

/// <summary>
/// <see cref="PasswordRules"/> as a data annotation, for request and form models. A null or empty
/// value passes, so an optional password (an OAuth or passkey signup) is left to
/// <c>[Required]</c> or the service.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AcceptablePasswordAttribute : ValidationAttribute
{
    public AcceptablePasswordAttribute() : base(PasswordRules.Message)
    {
    }

    public override bool IsValid(object? value) =>
        value is not string password || password.Length == 0 || PasswordRules.IsAcceptable(password);
}
