namespace Famick.HomeManagement.Core.DTOs.CloudLogin;

/// <summary>
/// Returned by <c>GET /api/profile/cloud-login/status</c> so the Razor
/// toggle can render its initial state without an extra round-trip.
/// </summary>
public sealed class CloudLoginStatusResponse
{
    /// <summary>True when this home server is paired with AuthProxy at all.
    /// When false the UI hides the toggle — opting in is meaningless.</summary>
    public bool ServerIsPaired { get; set; }

    /// <summary>True when the current user has opted in to cloud login.</summary>
    public bool UserIsOptedIn { get; set; }
}
