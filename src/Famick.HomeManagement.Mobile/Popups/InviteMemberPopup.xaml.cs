using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class InviteMemberPopup : Popup<InviteMemberResult>
{
    public InviteMemberPopup()
    {
        InitializeComponent();
    }

    public void Configure(string? email, string? phone, string displayName)
    {
        TitleLabel.Text = $"Invite {displayName}";
        if (!string.IsNullOrEmpty(email))
            EmailEntry.Text = email;
        if (!string.IsNullOrEmpty(phone))
            PhoneEntry.Text = phone;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await CloseAsync(null!);
    }

    private async void OnSendInviteClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();
        var phone = PhoneEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorLabel.Text = "Email is required";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            ErrorLabel.Text = "Phone number is required";
            ErrorLabel.IsVisible = true;
            return;
        }

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            if (addr.Address != email)
            {
                ErrorLabel.Text = "Please enter a valid email";
                ErrorLabel.IsVisible = true;
                return;
            }
        }
        catch
        {
            ErrorLabel.Text = "Please enter a valid email";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        await CloseAsync(new InviteMemberResult(email, phone));
    }
}
