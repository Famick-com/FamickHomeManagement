namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Stores and retrieves tenant information using SecureStorage.
/// </summary>
public class TenantStorage
{
    private const string TenantNameKey = "tenant_name";
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
    /// Clears the stored tenant name.
    /// </summary>
    public void Clear()
    {
        try
        {
            SecureStorage.Default.Remove(TenantNameKey);
        }
        catch
        {
            // Ignore
        }
    }
}
