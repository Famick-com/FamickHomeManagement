using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android implementation of Apple Sign in.
/// Native Apple Sign in is not available on Android - use web OAuth flow instead.
/// </summary>
public class AppleSignInService : IAppleSignInService
{
    /// <summary>
    /// Native Apple Sign in is not available on Android.
    /// </summary>
    public bool IsAvailable => false;

    public Task<AppleSignInCredential> SignInAsync()
    {
        throw new AppleSignInException(
            "Native Apple Sign in is not available on Android. Use web OAuth flow instead.",
            AppleSignInErrorCode.NotAvailable);
    }
}
