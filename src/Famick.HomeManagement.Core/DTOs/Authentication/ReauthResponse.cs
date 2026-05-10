namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Phase 2 — response from <c>POST /api/auth/reauth</c>. Returns a fresh access
/// token with <c>auth_time = now</c>; the refresh token is intentionally not
/// rotated so the client keeps its existing session.
/// </summary>
public class ReauthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
