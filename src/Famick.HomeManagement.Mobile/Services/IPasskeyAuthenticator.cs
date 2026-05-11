namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Phase 2.5b — native WebAuthn assertion bridge. Each platform implementation
/// (iOS: ASAuthorizationPlatformPublicKeyCredentialProvider, Android:
/// androidx.credentials.CredentialManager) drives the OS-level passkey
/// ceremony and returns the resulting assertion as a WebAuthn-spec JSON
/// string the server's /api/auth/passkey/authenticate/verify endpoint
/// accepts.
///
/// The bridge intentionally only handles the native ceremony — fetching
/// authentication options from the server, submitting the verification,
/// and writing the resulting tokens all live in StepUpReauthPage so the
/// bridge stays focused and easy to swap per-platform.
/// </summary>
public interface IPasskeyAuthenticator
{
    /// <summary>
    /// True when the device's OS version supports platform passkey APIs.
    /// iOS 16+ on iOS; API 28+ on Android. Used to hide the "Use Passkey"
    /// button on older devices without bumping the project's minSdk.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Runs the platform's passkey assertion ceremony.
    /// </summary>
    /// <param name="serializedOptionsJson">
    /// The <c>Options</c> field from <c>PasskeyAuthenticateOptionsResponse</c> —
    /// a serialized WebAuthn <c>PublicKeyCredentialRequestOptions</c> JSON blob.
    /// </param>
    /// <param name="cancellationToken">Cancels the ceremony (UI dismissed, etc.).</param>
    /// <returns>
    /// The assertion as a WebAuthn <c>AuthenticationResponseJSON</c> ready to
    /// submit to <c>/api/auth/passkey/authenticate/verify</c>, or <c>null</c>
    /// if the user cancelled or no matching passkey exists on the device.
    /// </returns>
    Task<string?> AuthenticateAsync(string serializedOptionsJson, CancellationToken cancellationToken = default);
}
