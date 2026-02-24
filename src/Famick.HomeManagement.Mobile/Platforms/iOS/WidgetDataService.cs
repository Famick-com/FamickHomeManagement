#if IOS
using Foundation;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Product item for sharing with the widget via App Group UserDefaults.
/// </summary>
public class WidgetProductItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public string? BestBeforeDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public bool IsExpired { get; set; }
}

/// <summary>
/// Service for sharing data with iOS widgets via App Group shared container.
/// </summary>
public static class WidgetDataService
{
    private const string AppGroupId = "group.com.famick.homemanagement";

    /// <summary>
    /// Updates widget data in the shared App Group container.
    /// </summary>
    /// <param name="expiringCount">Number of expired items</param>
    /// <param name="dueSoonCount">Number of items expiring soon</param>
    public static void UpdateWidgetData(int expiringCount, int dueSoonCount)
    {
        try
        {
            var userDefaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
            if (userDefaults == null)
            {
                Console.WriteLine("[WidgetDataService] Failed to access App Group container");
                return;
            }

            userDefaults.SetInt(expiringCount, "ExpiringItemCount");
            userDefaults.SetInt(dueSoonCount, "DueSoonItemCount");
            userDefaults.SetDouble(NSDate.Now.SecondsSince1970, "LastUpdated");
            userDefaults.Synchronize();

            Console.WriteLine($"[WidgetDataService] Updated widget data: Expiring={expiringCount}, DueSoon={dueSoonCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WidgetDataService] Error updating widget data: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates widget data including expiring product details for Lock Screen widgets.
    /// </summary>
    public static void UpdateWidgetDataWithProducts(int expiringCount, int dueSoonCount, List<WidgetProductItem> products)
    {
        try
        {
            var userDefaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
            if (userDefaults == null)
            {
                Console.WriteLine("[WidgetDataService] Failed to access App Group container");
                return;
            }

            // Keep existing integer keys for backward compat with Home Screen widget
            userDefaults.SetInt(expiringCount, "ExpiringItemCount");
            userDefaults.SetInt(dueSoonCount, "DueSoonItemCount");
            userDefaults.SetDouble(NSDate.Now.SecondsSince1970, "LastUpdated");

            // Serialize top 5 expiring products as JSON for Lock Screen widget
            var json = System.Text.Json.JsonSerializer.Serialize(products.Take(5));
            userDefaults.SetString(json, "WidgetExpiringProducts");

            userDefaults.Synchronize();

            Console.WriteLine($"[WidgetDataService] Updated widget data with {products.Count} products: Expiring={expiringCount}, DueSoon={dueSoonCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WidgetDataService] Error updating widget data: {ex.Message}");
        }
    }
}
#endif
