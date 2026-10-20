using System.Text.Json;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Reads where a <c>SIGNUP_REQUIRED</c> answer from an external sign-in continues.
/// </summary>
public static class SignupContinuation
{
    /// <summary>
    /// The same-origin path a <c>SIGNUP_REQUIRED</c> answer continues at, or null. Anything that is
    /// not a plain local path is ignored, so the answer cannot send the browser elsewhere.
    /// </summary>
    /// <remarks>
    /// Control characters are refused as well as <c>//</c> and <c>\</c>: browsers strip tab, CR and
    /// LF from a URL before parsing it, so <c>/\t/host</c> would otherwise arrive as <c>//host</c>.
    /// </remarks>
    public static string? LocalContinueUrl(ApiResult result)
    {
        if (result.ErrorCode != "SIGNUP_REQUIRED"
            || result.ErrorBody is not { ValueKind: JsonValueKind.Object } body
            || !body.TryGetProperty("signup", out var signup)
            || signup.ValueKind != JsonValueKind.Object
            || !signup.TryGetProperty("continueUrl", out var urlProp)
            || urlProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return IsLocalPath(urlProp.GetString()) ? urlProp.GetString() : null;
    }

    /// <summary>A path on this origin: starts with one <c>/</c>, no <c>\</c>, no control characters.</summary>
    public static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.Contains('\\')
        && !url.Any(char.IsControl);
}
