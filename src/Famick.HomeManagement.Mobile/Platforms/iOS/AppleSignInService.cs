using System.Text;
using AuthenticationServices;
using Foundation;
using UIKit;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS implementation of Apple Sign in using ASAuthorizationAppleIdProvider.
/// </summary>
public class AppleSignInService : NSObject, IAppleSignInService, IASAuthorizationControllerDelegate, IASAuthorizationControllerPresentationContextProviding
{
    private TaskCompletionSource<AppleSignInCredential>? _tcs;

    public bool IsAvailable => UIDevice.CurrentDevice.CheckSystemVersion(13, 0);

    public Task<AppleSignInCredential> SignInAsync()
    {
        if (!IsAvailable)
        {
            throw new AppleSignInException(
                "Sign in with Apple requires iOS 13 or later",
                AppleSignInErrorCode.NotAvailable);
        }

        _tcs = new TaskCompletionSource<AppleSignInCredential>();

        var provider = new ASAuthorizationAppleIdProvider();
        var request = provider.CreateRequest();
        request.RequestedScopes = new[] { ASAuthorizationScope.Email, ASAuthorizationScope.FullName };

        var controller = new ASAuthorizationController(new[] { request })
        {
            Delegate = this,
            PresentationContextProvider = this
        };

        controller.PerformRequests();

        return _tcs.Task;
    }

    #region IASAuthorizationControllerDelegate

    [Export("authorizationController:didCompleteWithAuthorization:")]
    public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
    {
        if (authorization.GetCredential<ASAuthorizationAppleIdCredential>() is not { } credential)
        {
            _tcs?.TrySetException(new AppleSignInException(
                "Invalid credential type received",
                AppleSignInErrorCode.InvalidResponse));
            return;
        }

        var identityToken = credential.IdentityToken != null
            ? Encoding.UTF8.GetString(credential.IdentityToken.ToArray())
            : null;

        var authorizationCode = credential.AuthorizationCode != null
            ? Encoding.UTF8.GetString(credential.AuthorizationCode.ToArray())
            : null;

        if (string.IsNullOrEmpty(identityToken))
        {
            _tcs?.TrySetException(new AppleSignInException(
                "No identity token received",
                AppleSignInErrorCode.InvalidResponse));
            return;
        }

        var result = new AppleSignInCredential
        {
            UserIdentifier = credential.User,
            IdentityToken = identityToken,
            AuthorizationCode = authorizationCode,
            Email = credential.Email,
            GivenName = credential.FullName?.GivenName,
            FamilyName = credential.FullName?.FamilyName
        };

        _tcs?.TrySetResult(result);
    }

    [Export("authorizationController:didCompleteWithError:")]
    public void DidComplete(ASAuthorizationController controller, NSError error)
    {
        var errorCode = (ASAuthorizationError)(int)error.Code;

        var signInErrorCode = errorCode switch
        {
            ASAuthorizationError.Canceled => AppleSignInErrorCode.Canceled,
            ASAuthorizationError.InvalidResponse => AppleSignInErrorCode.InvalidResponse,
            ASAuthorizationError.NotHandled => AppleSignInErrorCode.NotHandled,
            ASAuthorizationError.Failed => AppleSignInErrorCode.Failed,
            ASAuthorizationError.Unknown => AppleSignInErrorCode.Unknown,
            _ => AppleSignInErrorCode.Unknown
        };

        if (signInErrorCode == AppleSignInErrorCode.Canceled)
        {
            _tcs?.TrySetCanceled();
        }
        else
        {
            _tcs?.TrySetException(new AppleSignInException(
                error.LocalizedDescription ?? "Apple Sign in failed",
                signInErrorCode));
        }
    }

    #endregion

    #region IASAuthorizationControllerPresentationContextProviding

    public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
    {
        var window = UIApplication.SharedApplication.KeyWindow;

        // For iOS 15+, use the first connected scene's window
        if (window == null && UIDevice.CurrentDevice.CheckSystemVersion(15, 0))
        {
            var scenes = UIApplication.SharedApplication.ConnectedScenes;
            foreach (var scene in scenes)
            {
                if (scene is UIWindowScene windowScene)
                {
                    window = windowScene.Windows.FirstOrDefault(w => w.IsKeyWindow)
                        ?? windowScene.Windows.FirstOrDefault();
                    if (window != null) break;
                }
            }
        }

        return window ?? throw new InvalidOperationException("No window available for presentation");
    }

    #endregion
}
