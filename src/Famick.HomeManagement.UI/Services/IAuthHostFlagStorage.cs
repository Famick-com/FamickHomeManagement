namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Phase 5 chunk 5.K — persists the last-known value of the server's
/// <c>use_auth_famick_com</c> feature flag so the SPA can route auth
/// traffic to <c>auth.famick.com</c> when the flag is on.
///
/// Refreshed from <c>/api/auth/external/config</c> on each call to
/// <c>IApiClient.GetAuthConfigurationAsync</c>. Read by
/// <see cref="AuthHostRoutingHandler"/> on every outgoing request.
///
/// The cloud Blazor WASM client provides a localStorage-backed impl so
/// the flag survives reloads; self-hosted leaves this unregistered and
/// the handler's default (false) keeps everything on the same host.
/// </summary>
public interface IAuthHostFlagStorage
{
    Task<bool> GetUseAuthFamickComAsync();
    Task SetUseAuthFamickComAsync(bool value);
}
