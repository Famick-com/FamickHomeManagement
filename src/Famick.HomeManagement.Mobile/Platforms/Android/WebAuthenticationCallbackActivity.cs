using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Famick.HomeManagement.Mobile;

/// <summary>
/// Activity to receive OAuth callbacks from external authentication providers.
/// This activity intercepts the callback URL and passes control back to WebAuthenticator.
/// </summary>
[Activity(
    NoHistory = true,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.famick.homemanagement",
    DataHost = "oauth",
    DataPath = "/callback")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
