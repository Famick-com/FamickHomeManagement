namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Identity extracted from Home Assistant Ingress request headers.
/// </summary>
/// <param name="HaUserId">
/// Value of the <c>X-Remote-User-Id</c> header — HA's user GUID, stable across
/// HA restarts. Used as the <c>ProviderUserId</c> on the resulting
/// <c>UserExternalLogin</c> row.
/// </param>
/// <param name="Username">Value of the <c>X-Remote-User-Name</c> header (HA username), or null when absent.</param>
/// <param name="DisplayName">Value of the <c>X-Remote-User-Display-Name</c> header (HA display name), or null when absent.</param>
public sealed record HaIngressIdentity(
    string HaUserId,
    string? Username,
    string? DisplayName);
