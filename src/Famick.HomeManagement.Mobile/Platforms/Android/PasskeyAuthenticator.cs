using System.Runtime.Versioning;
using AndroidX.Credentials;
using Famick.HomeManagement.Mobile.Services;
using Java.Util.Concurrent;
using JavaObject = Java.Lang.Object;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Phase 2.5b — Android implementation of <see cref="IPasskeyAuthenticator"/>
/// using <c>androidx.credentials.CredentialManager</c> (API 28+).
///
/// CredentialManager dispatches the ceremony to a registered provider (in
/// practice, Google Play Services via the <c>Xamarin.AndroidX.Credentials.PlayServicesAuth</c>
/// NuGet referenced in the csproj). The OS shows the system passkey sheet,
/// the user picks a passkey, and the provider returns a WebAuthn-spec
/// <c>AuthenticationResponseJSON</c> ready to submit to the server's
/// <c>/api/auth/passkey/authenticate/verify</c> endpoint.
///
/// Unlike iOS, Android takes the raw WebAuthn options JSON directly — no
/// client-side parsing required. The binding doesn't expose a Task-returning
/// overload, so we wire the standard 5-parameter <c>GetCredentialAsync</c>
/// callback through a <see cref="TaskCompletionSource{TResult}"/>.
/// </summary>
public sealed class PasskeyAuthenticator : IPasskeyAuthenticator
{
    public bool IsSupported => OperatingSystem.IsAndroidVersionAtLeast(28);

    public Task<string?> AuthenticateAsync(string serializedOptionsJson, CancellationToken cancellationToken = default)
    {
        // OperatingSystem.IsAndroidVersionAtLeast satisfies CA1416's trace into
        // the SupportedOSPlatform-annotated helper below — Build.VERSION.SdkInt
        // checks don't (analyzer can't reason about the enum comparison).
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return Task.FromResult<string?>(null);
        }

        return AuthenticateInternalAsync(serializedOptionsJson, cancellationToken);
    }

    [SupportedOSPlatform("android28.0")]
    private static Task<string?> AuthenticateInternalAsync(string serializedOptionsJson, CancellationToken cancellationToken)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return Task.FromResult<string?>(null);
        }

        var credentialManager = CredentialManager.Create(activity);
        var option = new GetPublicKeyCredentialOption(serializedOptionsJson);
        var request = new GetCredentialRequest.Builder()
            .AddCredentialOption(option)
            .Build()!;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new GetCredentialCallback(tcs);
        // The single-thread executor is fine — the callback completes a TCS
        // and immediately exits. RunContinuationsAsynchronously above hops the
        // continuation off this thread, so the executor doesn't need to be
        // the UI executor.
        var executor = Executors.NewSingleThreadExecutor()!;

        // Caller-side cancellation (modal dismissed) unblocks the waiter; the
        // OS ceremony's eventual callback completes the TCS again, but the
        // second TrySetResult is a no-op.
        cancellationToken.Register(() => tcs.TrySetResult(null));

        credentialManager.GetCredentialAsync(activity, request, null, executor, callback);

        return tcs.Task;
    }

    private sealed class GetCredentialCallback : JavaObject, ICredentialManagerCallback
    {
        private readonly TaskCompletionSource<string?> _tcs;

        public GetCredentialCallback(TaskCompletionSource<string?> tcs)
        {
            _tcs = tcs;
        }

        public void OnResult(JavaObject? result)
        {
            // AuthenticationResponseJson is already in WebAuthn spec shape —
            // matches what the iOS bridge serializes by hand and what the
            // web JS interop produces. Server endpoint is unchanged.
            if (result is GetCredentialResponse response
                && response.Credential is PublicKeyCredential pkc)
            {
                _tcs.TrySetResult(pkc.AuthenticationResponseJson);
            }
            else
            {
                _tcs.TrySetResult(null);
            }
        }

        public void OnError(JavaObject? error)
        {
            // GetCredentialException + subclasses: user cancellation, no
            // matching credential, no provider, etc. All surface as null so
            // the page can show its standard error UI or fall back to password.
            _tcs.TrySetResult(null);
        }
    }
}
