namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Stores and retrieves tenant information using SecureStorage.
/// </summary>
public class TenantStorage
{
    private const string TenantNameKey = "tenant_name";
    private const string SubscriptionTierKey = "subscription_tier";
    private const string IsTrialActiveKey = "is_trial_active";
    private const string IsExpiredKey = "is_expired";
    private const string DefaultAppTitle = "Shopping";

    /// <summary>
    /// Gets the stored tenant name.
    /// </summary>
    public async Task<string?> GetTenantNameAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TenantNameKey).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stores the tenant name.
    /// </summary>
    public async Task SetTenantNameAsync(string? tenantName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tenantName))
            {
                SecureStorage.Default.Remove(TenantNameKey);
            }
            else
            {
                await SecureStorage.Default.SetAsync(TenantNameKey, tenantName).ConfigureAwait(false);
            }
        }
        catch
        {
            // SecureStorage may fail on some platforms, ignore
        }
    }

    /// <summary>
    /// Gets the stored subscription tier string.
    /// </summary>
    public string GetSubscriptionTier()
    {
        return Preferences.Default.Get(SubscriptionTierKey, string.Empty);
    }

    /// <summary>
    /// Gets whether the trial is active.
    /// </summary>
    public bool GetIsTrialActive()
    {
        return Preferences.Default.Get(IsTrialActiveKey, false);
    }

    /// <summary>
    /// Gets whether the subscription is expired.
    /// </summary>
    public bool GetIsExpired()
    {
        return Preferences.Default.Get(IsExpiredKey, false);
    }

    /// <summary>
    /// Stores subscription state from login response tenant info.
    /// </summary>
    public void SetSubscriptionState(string? tier, bool isTrialActive, bool isExpired)
    {
        Preferences.Default.Set(SubscriptionTierKey, tier ?? string.Empty);
        Preferences.Default.Set(IsTrialActiveKey, isTrialActive);
        Preferences.Default.Set(IsExpiredKey, isExpired);
    }

    /// <summary>
    /// Gets the app title based on tenant name.
    /// Returns "{TenantName} Shopping" or "Shopping" if no tenant.
    /// </summary>
    public async Task<string> GetAppTitleAsync()
    {
        var tenantName = await GetTenantNameAsync().ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantName)
            ? DefaultAppTitle
            : $"{tenantName} Shopping";
    }

    /// <summary>
    /// Clears the stored tenant name and subscription state.
    /// </summary>
    public void Clear()
    {
        try
        {
            SecureStorage.Default.Remove(TenantNameKey);
            Preferences.Default.Remove(SubscriptionTierKey);
            Preferences.Default.Remove(IsTrialActiveKey);
            Preferences.Default.Remove(IsExpiredKey);
        }
        catch
        {
            // Ignore
        }
    }
}
