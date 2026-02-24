using Android.Content;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android implementation of Google Sign-In.
/// Currently returns IsAvailable = false to use web OAuth flow instead.
///
/// Note: The Xamarin.GooglePlayServices.Auth NuGet package has some compatibility
/// issues with .NET 10 MAUI. Google's web OAuth flow works well for mobile apps,
/// so we use that instead.
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
            "Native Google Sign-In is not available on Android. Use web OAuth flow.",
            GoogleSignInErrorCode.NotAvailable);
    }

    public Task SignOutAsync()
    {
        // Nothing to sign out since we don't use native sign-in
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stub for handling activity result. Does nothing since native sign-in is not available.
    /// Kept for future implementation when SDK bindings are available.
    /// </summary>
    public static void HandleActivityResult(int requestCode, global::Android.App.Result resultCode, Intent? data)
    {
        // No-op: Native Google Sign-In not available
    }
}
