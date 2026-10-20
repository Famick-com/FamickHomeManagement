using System.Text.Json;
using Famick.HomeManagement.Shared.Net;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Reads where a <c>SIGNUP_REQUIRED</c> answer from an external sign-in continues.
/// </summary>
public static class SignupContinuation
{
    /// <summary>
    /// The same-origin path a <c>SIGNUP_REQUIRED</c> answer continues at, or null. The path goes
    /// through <paramref name="validator"/>, the same check as every <c>returnUrl</c>, and only a
    /// path on this origin is accepted; an allow-listed absolute URL is not.
    /// </summary>
    public static string? LocalContinueUrl(ApiResult result, IRedirectUrlValidator validator)
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

        return validator.TryValidate(urlProp.GetString(), out var safe, out _)
               && safe is not null
               && safe.StartsWith('/')
            ? safe
            : null;
    }
}
