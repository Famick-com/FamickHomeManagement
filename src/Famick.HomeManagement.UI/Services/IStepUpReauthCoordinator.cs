namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Phase 2.5 — orchestrates the step-up re-auth modal flow on Blazor WASM.
///
/// When <see cref="HttpApiClient"/> receives a 403 with
/// <c>code = "STEP_UP_REQUIRED"</c>, it calls <see cref="RequestStepUpAsync"/>.
/// The coordinator shows a modal (password input + optional passkey button),
/// handles the resulting <c>POST /api/auth/reauth</c> (or passkey verify),
/// writes the new access token via <see cref="ITokenStorage.SetAccessTokenAsync"/>,
/// and returns <c>true</c> so the HTTP client retries the original request.
/// Returns <c>false</c> when the user dismisses the modal — the original 403
/// then surfaces to the caller normally.
///
/// Registered only on Blazor WASM (Web.Client/Program.cs); on platforms where
/// no coordinator is registered, <see cref="HttpApiClient"/> tolerates the
/// missing dependency and lets 403 STEP_UP_REQUIRED surface as a normal 403.
/// </summary>
public interface IStepUpReauthCoordinator
{
    Task<bool> RequestStepUpAsync(CancellationToken cancellationToken = default);
}
