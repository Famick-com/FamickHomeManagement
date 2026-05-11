using System.Runtime.Versioning;
using System.Text.Json;
using AuthenticationServices;
using Famick.HomeManagement.Mobile.Services;
using Foundation;
using UIKit;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Phase 2.5b — iOS implementation of <see cref="IPasskeyAuthenticator"/> using
/// <c>ASAuthorizationPlatformPublicKeyCredentialProvider</c> (iOS 16+).
///
/// Driven by <c>ASAuthorizationController</c> — same controller machinery used by
/// <c>AppleSignInService</c>. We register as the delegate and presentation
/// context provider, build a passkey assertion request from the server-supplied
/// WebAuthn options, then await the controller's success or error callback and
/// translate the result into the WebAuthn <c>AuthenticationResponseJSON</c>
/// shape the server's <c>/api/auth/passkey/authenticate/verify</c> endpoint
/// expects.
/// </summary>
public sealed class PasskeyAuthenticator
    : NSObject,
      IPasskeyAuthenticator,
      IASAuthorizationControllerDelegate,
      IASAuthorizationControllerPresentationContextProviding
{
    private TaskCompletionSource<string?>? _tcs;

    public bool IsSupported => OperatingSystem.IsIOSVersionAtLeast(16, 0);

    public Task<string?> AuthenticateAsync(string serializedOptionsJson, CancellationToken cancellationToken = default)
    {
        // Inline the OS check rather than going through IsSupported — the
        // CA1416 analyzer can't trace property accessors to OperatingSystem.* calls.
        if (!OperatingSystem.IsIOSVersionAtLeast(16, 0))
        {
            return Task.FromResult<string?>(null);
        }

        return AuthenticateInternalAsync(serializedOptionsJson, cancellationToken);
    }

    [SupportedOSPlatform("ios16.0")]
    private Task<string?> AuthenticateInternalAsync(string serializedOptionsJson, CancellationToken cancellationToken)
    {
        // Parse the server-supplied WebAuthn PublicKeyCredentialRequestOptions.
        // Required fields: rpId, challenge. allowCredentials is intentionally
        // ignored on iOS — the AllowedCredentials setter was removed from
        // ASAuthorizationPlatformPublicKeyCredentialAssertionRequest in iOS 16.4
        // and no provider-level alternative is exposed in .NET bindings. The
        // OS shows all platform passkeys for the rpId; if the user picks one
        // the server doesn't know, the verify endpoint rejects it.
        string? rpId;
        byte[]? challenge;
        try
        {
            using var doc = JsonDocument.Parse(serializedOptionsJson);
            var root = doc.RootElement;

            rpId = root.TryGetProperty("rpId", out var rpIdEl) ? rpIdEl.GetString() : null;
            var challengeB64 = root.TryGetProperty("challenge", out var chEl) ? chEl.GetString() : null;
            challenge = Base64UrlDecode(challengeB64);
        }
        catch (JsonException)
        {
            return Task.FromResult<string?>(null);
        }

        if (string.IsNullOrEmpty(rpId) || challenge is null)
        {
            return Task.FromResult<string?>(null);
        }

        var provider = new ASAuthorizationPlatformPublicKeyCredentialProvider(rpId);
        var request = provider.CreateCredentialAssertionRequest(NSData.FromArray(challenge));

        _tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Caller-side cancellation (e.g. modal dismissed) unblocks the waiter
        // immediately; the OS ceremony will close on its own when no controller
        // delegate calls back.
        cancellationToken.Register(() => _tcs?.TrySetResult(null));

        var controller = new ASAuthorizationController(new ASAuthorizationRequest[] { request })
        {
            Delegate = this,
            PresentationContextProvider = this
        };
        controller.PerformRequests();

        return _tcs.Task;
    }

    [Export("authorizationController:didCompleteWithAuthorization:")]
    public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(16, 0))
        {
            _tcs?.TrySetResult(null);
            return;
        }

        var assertion = authorization.GetCredential<ASAuthorizationPlatformPublicKeyCredentialAssertion>();
        if (assertion is null
            || assertion.CredentialId is null
            || assertion.RawClientDataJson is null
            || assertion.Signature is null
            || assertion.RawAuthenticatorData is null)
        {
            _tcs?.TrySetResult(null);
            return;
        }

        var credentialIdB64 = Base64UrlEncode(assertion.CredentialId.ToArray());

        // WebAuthn AuthenticationResponseJSON shape — matches what
        // passkeyAuth.authenticate JS interop produces on the web side. The
        // server's /api/auth/passkey/authenticate/verify endpoint parses this
        // exact shape, so don't change field names without coordinating
        // with the server.
        var json = JsonSerializer.Serialize(new
        {
            id = credentialIdB64,
            rawId = credentialIdB64,
            type = "public-key",
            response = new
            {
                authenticatorData = Base64UrlEncode(assertion.RawAuthenticatorData.ToArray()),
                clientDataJSON = Base64UrlEncode(assertion.RawClientDataJson.ToArray()),
                signature = Base64UrlEncode(assertion.Signature.ToArray()),
                userHandle = assertion.UserId is not null ? Base64UrlEncode(assertion.UserId.ToArray()) : ""
            },
            authenticatorAttachment = "platform"
        });

        _tcs?.TrySetResult(json);
    }

    [Export("authorizationController:didCompleteWithError:")]
    public void DidComplete(ASAuthorizationController controller, NSError error)
    {
        // User cancellation, no matching passkey, biometric failure — all
        // surface as null so the page can show its standard error UI or fall
        // back to password reauth.
        _tcs?.TrySetResult(null);
    }

    public UIWindow GetPresentationAnchor(ASAuthorizationController controller)
    {
        var window = UIApplication.SharedApplication.KeyWindow;
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
        return window ?? throw new InvalidOperationException("No window available for passkey presentation");
    }

    private static byte[]? Base64UrlDecode(string? input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        var s = input.Replace('-', '+').Replace('_', '/');
        var pad = s.Length % 4;
        if (pad > 0) s = s.PadRight(s.Length + (4 - pad), '=');
        try { return Convert.FromBase64String(s); }
        catch (FormatException) { return null; }
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
