namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// A response that hands a refresh token to the client.
/// </summary>
/// <remarks>
/// The browser app asks for its refresh token as an HttpOnly cookie instead of in the body, so
/// script on the page can never read it. The server finds the token through this interface, moves
/// it into the cookie and clears it from the body. Other clients (the mobile app, server-to-server
/// callers) do not ask, and keep receiving it in the body.
/// </remarks>
public interface IRefreshTokenCarrier
{
    string? RefreshToken { get; set; }
}
