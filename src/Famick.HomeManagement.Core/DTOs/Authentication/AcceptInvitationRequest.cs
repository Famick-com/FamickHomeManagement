namespace Famick.HomeManagement.Core.DTOs.Authentication;

public class AcceptInvitationRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
