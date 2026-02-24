#if IOS
using Security;
using Foundation;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Writes JWT tokens to a shared keychain group so the widget extension can read them.
/// Uses SecAccessible.AfterFirstUnlock so the widget can access tokens while the device is locked.
/// </summary>
public static class SharedKeychainService
{
    private const string AccessGroup = "com.famick.homemanagement.shared";
    private const string ServiceName = "com.famick.homemanagement";
    private const string AccessTokenKey = "widget_access_token";
    private const string RefreshTokenKey = "widget_refresh_token";
    private const string BaseUrlKey = "widget_base_url";

    public static void SetSharedTokens(string accessToken, string refreshToken, string baseUrl)
    {
        try
        {
            SetKeychainItem(AccessTokenKey, accessToken);
            SetKeychainItem(RefreshTokenKey, refreshToken);
            SetKeychainItem(BaseUrlKey, baseUrl);
            Console.WriteLine("[SharedKeychainService] Tokens written to shared keychain");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SharedKeychainService] Error writing tokens: {ex.Message}");
        }
    }

    public static void ClearSharedTokens()
    {
        try
        {
            DeleteKeychainItem(AccessTokenKey);
            DeleteKeychainItem(RefreshTokenKey);
            DeleteKeychainItem(BaseUrlKey);
            Console.WriteLine("[SharedKeychainService] Shared keychain tokens cleared");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SharedKeychainService] Error clearing tokens: {ex.Message}");
        }
    }

    private static void SetKeychainItem(string key, string value)
    {
        // Delete existing item first (update = delete + add)
        DeleteKeychainItem(key);

        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = ServiceName,
            Account = key,
            ValueData = NSData.FromString(value, NSStringEncoding.UTF8),
            AccessGroup = AccessGroup,
            Accessible = SecAccessible.AfterFirstUnlock,
            Synchronizable = false
        };

        var status = SecKeyChain.Add(record);
        if (status != SecStatusCode.Success)
        {
            Console.WriteLine($"[SharedKeychainService] Failed to add keychain item '{key}': {status}");
        }
    }

    private static void DeleteKeychainItem(string key)
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = ServiceName,
            Account = key,
            AccessGroup = AccessGroup
        };

        SecKeyChain.Remove(query);
    }
}
#endif
