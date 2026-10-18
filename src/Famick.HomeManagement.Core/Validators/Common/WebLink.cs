using FluentValidation;
using FluentValidation.Results;

namespace Famick.HomeManagement.Core.Validators.Common;

/// <summary>
/// Rules for links people type in and the apps later open: social-media profiles, property and
/// HOA links, manufacturer links, recipe videos.
/// </summary>
/// <remarks>
/// Only absolute <c>http</c> and <c>https</c> URLs with a host are links here. Anything else
/// (<c>javascript:</c>, <c>data:</c>, <c>file:</c>, an app's own scheme) would run or launch
/// something when clicked rather than open a page, so it is refused on save and not rendered as a
/// link on display. <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> alone is not enough:
/// <c>javascript:alert(1)</c> is a perfectly good absolute URI.
/// </remarks>
public static class WebLink
{
    public static bool IsWebUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>The link if it is a web URL, otherwise null: for rendering a stored value as an href.</summary>
    public static string? SafeHref(string? value) => IsWebUrl(value) ? value!.Trim() : null;

    /// <summary>
    /// Throws a validation error (a 400 naming <paramref name="propertyName"/>) unless
    /// <paramref name="value"/> is empty or a web URL.
    /// </summary>
    public static void Ensure(string? value, string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsWebUrl(value))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(propertyName, Message(propertyName)),
            });
        }
    }

    /// <summary>Requires an empty value or a web URL.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeWebUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) || IsWebUrl(value))
            .WithMessage(x => "Must be a web address starting with http:// or https://");

    private static string Message(string propertyName) =>
        $"{propertyName} must be a web address starting with http:// or https://";
}
