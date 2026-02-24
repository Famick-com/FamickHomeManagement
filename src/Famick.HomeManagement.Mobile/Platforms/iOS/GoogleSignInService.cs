using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS implementation of Google Sign-In.
/// Currently returns IsAvailable = false to use web OAuth flow instead.
///
/// Note: The Xamarin.Google.iOS.SignIn NuGet package has compatibility issues
/// with .NET 10 MAUI (missing simulator binaries). Google's web OAuth flow
/// works well for mobile apps, so we use that instead.
///
/// When better SDK bindings become available, this can be updated to use
/// the native Google Sign-In SDK.
/// </summary>
public class GoogleSignInService : IGoogleSignInService
{
    /// <summary>
    /// Returns false - native Google Sign-In is not available.
    /// App will fall back to web OAuth flow.
    /// </summary>
    public bool IsAvailable => false;

    public Task<GoogleSignInCredential> SignInAsync()
    {
        throw new GoogleSignInException(
            "Native Google Sign-In is not available on iOS. Use web OAuth flow.",
            GoogleSignInErrorCode.NotAvailable);
    }

    public Task SignOutAsync()
    {
        // Nothing to sign out since we don't use native sign-in
        return Task.CompletedTask;
    }
}
