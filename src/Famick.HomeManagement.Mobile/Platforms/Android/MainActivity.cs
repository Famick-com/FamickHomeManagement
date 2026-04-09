using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Famick.HomeManagement.Mobile.Platforms.Android;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "famickshopping",
    DataHost = "shopping")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "famick")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "app.famick.com",
    DataPathPrefix = "/storage/",
    AutoVerify = true)]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/x-vcard")]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/vcard")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        if (intent?.Action == Intent.ActionSend)
        {
            HandleSharedContact(intent);
            return;
        }

        if (intent?.Data != null)
        {
            HandleDeepLink(intent);
        }
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Create notification channel for push notifications (Android 8+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                "famick_default",
                "Famick Notifications",
                NotificationImportance.Default);
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }

        // Schedule periodic contact sync
        ContactSyncWorker.Schedule();

        // Handle shared contact if app was opened via share intent
        if (Intent?.Action == Intent.ActionSend)
        {
            HandleSharedContact(Intent);
            return;
        }

        // Handle deep link if app was opened via deep link
        if (Intent?.Data != null)
        {
            HandleDeepLink(Intent);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        // Handle Google Sign-In result
        GoogleSignInService.HandleActivityResult(requestCode, resultCode, data);

        // Handle contact picker result
        DeviceContactPicker.HandleActivityResult(requestCode, resultCode, data);
    }

    private void HandleSharedContact(Intent intent)
    {
        try
        {
            var stream = intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;
            if (stream == null)
            {
                // Try ClipData as fallback
                if (intent.ClipData?.ItemCount > 0)
                    stream = intent.ClipData.GetItemAt(0)?.Uri;
            }

            if (stream == null)
                return;

            using var inputStream = ContentResolver?.OpenInputStream(stream);
            if (inputStream == null)
                return;

            using var reader = new System.IO.StreamReader(inputStream);
            var vCardText = reader.ReadToEnd();

            var contactData = VCardParser.Parse(vCardText);
            if (contactData != null)
            {
                App.PendingSharedContact = contactData;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainActivity] Error handling shared contact: {ex.Message}");
        }
    }

    private void HandleDeepLink(Intent intent)
    {
        var uri = intent.Data;
        if (uri == null) return;

        var scheme = uri.Scheme;

        // Handle https:// deep links (Universal Links / App Links)
        if (scheme == "https")
        {
            var netUri = new Uri(uri.ToString() ?? string.Empty);
            App.HandleDeepLink(netUri);
            return;
        }

        // Handle famick:// deep links (verification, setup, etc.)
        if (scheme == "famick")
        {
            var netUri = new Uri(uri.ToString() ?? string.Empty);
            App.HandleDeepLink(netUri);
            return;
        }

        // Parse the deep link: famickshopping://shopping/session?ListId={guid}&ListName={name}
        var path = uri.Path;
        var listId = uri.GetQueryParameter("ListId");
        var listName = uri.GetQueryParameter("ListName");

        if (!string.IsNullOrEmpty(listId) && Guid.TryParse(listId, out var parsedListId))
        {
            // Navigate to the shopping session page
            // The App.xaml.cs will handle this via MessagingCenter or a static property
            App.PendingDeepLink = new DeepLinkInfo
            {
                ListId = parsedListId,
                ListName = listName ?? "Shopping"
            };
        }
    }
}
