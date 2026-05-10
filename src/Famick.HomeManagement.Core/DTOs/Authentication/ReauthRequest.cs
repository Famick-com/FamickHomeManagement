namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Phase 2 — body for <c>POST /api/auth/reauth</c>. The user is already
/// authenticated (the endpoint requires <c>[Authorize]</c>); they re-supply
/// their password to refresh <c>auth_time</c> on a newly issued access token
/// without rotating the refresh-token family.
/// </summary>
public class ReauthRequest
{
    /// <summary>The currently-authenticated user's password.</summary>
    public string Password { get; set; } = string.Empty;
}
